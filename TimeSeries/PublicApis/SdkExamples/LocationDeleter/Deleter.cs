using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using LocationDeleter.PrivateApis.Processor;
using LocationDeleter.PrivateApis.SiteVisit;
using ServiceStack;
using ServiceStack.Logging;
using Publish3x = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x;

namespace LocationDeleter
{
    public class Deleter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }
        private IAquariusClient Client { get; set; }
        private List<LocationInfo> ResolvedLocations { get; set; }
        private int InspectedLocations { get; set; }
        private int InspectedTimeSeries { get; set; }
        private int InspectedDerivedTimeSeries { get; set; }
        private int InspectedFieldVisits { get; set; }
        private int InspectedThresholds { get; set; }
        private int InspectedRatingModels { get; set; }
        private int InspectedSensors { get; set; }
        private int InspectedAttachments { get; set; }

        public void Run()
        {
            Log.Info($"Connecting {GetExecutingFileVersion()} to {Context.Server} ...");

            using (Client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} ({Client.ServerVersion})");

                RegisterPrivateClients();
                AdaptToSpecificVersion();

                ResolvedLocations = GetResolvedLocations();

                DeleteSpecifiedFieldVisits();
                DeleteSpecifiedTimeSeries();
                DeleteSpecifiedLocations();
            }
        }

        private static string GetExecutingFileVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // ReSharper disable once PossibleNullReferenceException
            return $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} v{fileVersionInfo.FileVersion}";
        }

        private void DeleteSpecifiedFieldVisits()
        {
            if (!IsFieldVisitDeletionEnabled())
                return;

            if (Is3X())
                throw new ExpectedException($"Field visit deletion is not supported for AQTS {Client.ServerVersion}");

            if (!ResolvedLocations.Any())
            {
                // Delete field visits from all locations
                Context.LocationsToDelete = new List<string>{"*"};

                ResolvedLocations = GetResolvedLocations();
            }

            var timeRange = new List<string>();

            if (Context.VisitsBefore.HasValue)
            {
                timeRange.Add($"before {Context.VisitsBefore.Value:O}");
            }

            if (Context.VisitsAfter.HasValue)
            {
                timeRange.Add($"after {Context.VisitsAfter.Value:O}");
            }

            var locationQuantity = "location".ToQuantity(ResolvedLocations.Count);

            Log.Info($"Inspecting {locationQuantity} for field visits {string.Join(" and ", timeRange)} ...");

            var deletedVisitCount = 0;

            foreach (var locationInfo in ResolvedLocations)
            {
                deletedVisitCount += DeleteVisitsAtLocation(locationInfo);
            }

            if (Context.DryRun)
                Log.Info($"Dry run completed. {"field visit".ToQuantity(InspectedFieldVisits)} would have been deleted from {locationQuantity}.");
            else
                Log.Info($"Deleted {"field visit".ToQuantity(deletedVisitCount)} from {locationQuantity}.");
        }

        private bool IsFieldVisitDeletionEnabled()
        {
            return Context.VisitsBefore.HasValue || Context.VisitsAfter.HasValue;
        }

        private int DeleteVisitsAtLocation(LocationInfo locationInfo)
        {
            var siteVisitLocation = GetSiteVisitLocation(locationInfo);

            var visits = _siteVisit.Get(new GetLocationVisits
            {
                Id = siteVisitLocation.Id,
                StartTime = Context.VisitsAfter?.UtcDateTime,
                EndTime = Context.VisitsBefore?.UtcDateTime
            });

            if (!visits.Any())
            {
                Log.Info($"No field visits to delete in location '{locationInfo.Identifier}'.");
                return 0;
            }

            InspectedFieldVisits += visits.Count;
            var visitQuantity = $"{"field visit".ToQuantity(visits.Count)} at location '{locationInfo.Identifier}'";
            var startDate = visits.First().StartDate;
            var endDate = visits.Last().EndDate;
            var visitSummary = $"{visitQuantity} from {startDate} to {endDate}";

            if (!ConfirmAction(
                $"deletion of {visitQuantity}",
                $"delete {visitQuantity}",
                () => visitSummary,
                $"the identifier of the location",
                locationInfo.Identifier))
            {
                return 0;
            }

            foreach (var visit in visits)
            {
                _siteVisit.Delete(new DeleteVisit {Id = visit.Id});
                Log.Info($"Deleted '{locationInfo.Identifier}' visit Start={visit.StartDate} End={visit.EndDate}");
            }

            Log.Info($"Deleted {visitSummary} successfully.");

            return visits.Count;
        }

        private void DeleteSpecifiedTimeSeries()
        {
            if (!Context.TimeSeriesToDelete.Any())
                return;

            if (Is3X())
                throw new ExpectedException($"Time-series deletion is not supported for AQTS {Client.ServerVersion}");

            var deletedTimeSeriesCount = 0;

            foreach (var timeSeriesIdentifier in Context.TimeSeriesToDelete)
            {
                deletedTimeSeriesCount += DeleteTimeSeries(timeSeriesIdentifier);
            }

            if (Context.DryRun)
                Log.Info($"Dry run complete. {InspectedTimeSeries} time-series would have been deleted.");
            else
                Log.Info($"Deleted {deletedTimeSeriesCount} of {Context.TimeSeriesToDelete.Count} time-series.");
        }

        private int DeleteTimeSeries(string timeSeriesIdentifierOrGuid)
        {
            var timeSeriesDescription = GetTimeSeriesDescription(timeSeriesIdentifierOrGuid);

            if (timeSeriesDescription == null)
                return 0;

            ++InspectedTimeSeries;

            if (!ConfirmAction(
                $"deletion of '{timeSeriesDescription.Identifier}'",
                $"delete {timeSeriesDescription.Identifier}",
                () => GetTimeSeriesSummary(timeSeriesDescription),
                $"the identifier of the time-series",
                timeSeriesDescription.Identifier))
            {
                return 0;
            }

            Log.Info($"Deleting '{timeSeriesDescription.Identifier}' ...");
            DeleteTimeSeries(timeSeriesDescription);
            Log.Info($"Deleted '{timeSeriesDescription.Identifier}' successfully.");

            return 1;
        }

        private void DeleteTimeSeries(TimeSeriesDescription timeSeriesDescription)
        {
            if (timeSeriesDescription.TimeSeriesType == "Reflected")
            {
                Client.Provisioning.Delete(new DeleteTimeSeries { TimeSeriesUniqueId = timeSeriesDescription.UniqueId });
                return;
            }

            var locationInfo = ResolveLocationInfoNg(timeSeriesDescription.LocationIdentifier).Single();
            var siteVisitLocation = GetSiteVisitLocation(locationInfo);
            var timeSeries = _processor.Get(new GetTimeSeriesForLocationRequest { LocationId = siteVisitLocation.Id })
                .Single(ts => ts.UniqueId == timeSeriesDescription.UniqueId);

            _processor.Delete(new DeleteTimeSeriesRequest {TimeSeriesId = timeSeries.TimeSeriesId});
        }

        private TimeSeriesDescription GetTimeSeriesDescription(string timeSeriesIdentifierOrGuid)
        {
            if (Guid.TryParse(timeSeriesIdentifierOrGuid, out var uniqueId))
            {
                try
                {
                    return Client.Publish.Get(new TimeSeriesDescriptionListByUniqueIdServiceRequest
                        {
                            TimeSeriesUniqueIds = new List<Guid> {uniqueId}
                        })
                        .TimeSeriesDescriptions
                        .Single();
                }
                catch (WebServiceException)
                {
                    Log.Warn($"'{uniqueId}' is not a known time-series unique ID");
                    return null;
                }
            }

            var locationIdentifier = TimeSeriesIdentifierParser.ParseLocationIdentifier(timeSeriesIdentifierOrGuid);

            var timeSeriesDescription = Client.Publish.Get(new TimeSeriesDescriptionServiceRequest
                {
                    LocationIdentifier = locationIdentifier
                })
                .TimeSeriesDescriptions
                .SingleOrDefault(ts => ts.Identifier.Equals(timeSeriesIdentifierOrGuid, StringComparison.InvariantCultureIgnoreCase));

            if (timeSeriesDescription == null)
            {
                Log.Warn($"Time-series '{timeSeriesIdentifierOrGuid}' not found.");
                return null;
            }

            return timeSeriesDescription;
        }

        private string GetTimeSeriesSummary(TimeSeriesDescription timeSeries)
        {
            var timeSeriesData = Client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeries.UniqueId
            });

            var pointsSummary = timeSeriesData.Points.Any()
                ? $"{"point".ToQuantity(timeSeriesData.Points.Count)} from {timeSeriesData.Points.First().Timestamp.DateTimeOffset} to {timeSeriesData.Points.Last().Timestamp.DateTimeOffset}"
                : "no points";

            var downchainDependencies = Client.Publish.Get(new DownchainProcessorListByTimeSeriesServiceRequest
            {
                TimeSeriesUniqueId = timeSeries.UniqueId
            }).Processors;

            var timeSeriesSummary = FriendlyListExcludingZeroCounts(
                pointsSummary,
                "threshold".ToQuantity(timeSeries.Thresholds.Count),
                "grade".ToQuantity(timeSeriesData.Grades.Count),
                "qualifier".ToQuantity(timeSeriesData.Qualifiers.Count),
                "note".ToQuantity(timeSeriesData.Notes.Count),
                "approval".ToQuantity(timeSeriesData.Approvals.Count),
                "derived dependency".ToQuantity(downchainDependencies.Count));

            return $"Time-series '{timeSeries.Identifier}' ({timeSeries.TimeSeriesType}) has {timeSeriesSummary}";
        }

        private static string FriendlyListExcludingZeroCounts(params string[] items)
        {
            return FriendlyList(items.Where(item => !item.StartsWith("0 ")).ToArray());
        }

        private static string FriendlyList(params string[] items)
        {
            switch (items.Length)
            {
                case 0:
                    return string.Empty;

                case 1:
                    return items[0];

                case 2:
                    return $"{items[0]} and {items[1]}";

                default:
                    return $"{string.Join(", ", items.Take(items.Length - 1))} and {items.Last()}";
            }
        }

        private void DeleteSpecifiedLocations()
        {
            if (ResolvedLocations.Count == 0 || IsFieldVisitDeletionEnabled())
                return;

            var deletedLocationCount = 0;

            foreach (var location in ResolvedLocations)
            {
                deletedLocationCount += DeleteLocation(location);
            }

            var deletionSummary = FriendlyListExcludingZeroCounts(
                TimeSeriesInventory(InspectedTimeSeries, InspectedDerivedTimeSeries),
                "rating model".ToQuantity(InspectedRatingModels),
                "threshold".ToQuantity(InspectedThresholds),
                "sensor".ToQuantity(InspectedSensors),
                "field visit".ToQuantity(InspectedFieldVisits),
                "attachment".ToQuantity(InspectedAttachments));

            if (Context.DryRun)
                Log.Info($"Dry run completed. {"location".ToQuantity(InspectedLocations)} would have been deleted, including {deletionSummary}.");
            else
                Log.Info($"Deleted {deletedLocationCount} of {"location".ToQuantity(ResolvedLocations.Count)}, including {deletionSummary}.");
        }

        private static string TimeSeriesInventory(int timeSeriesCount, int derivedTimeSeriesCount)
        {
            return $"{timeSeriesCount} time-series ({derivedTimeSeriesCount} derived, {timeSeriesCount - derivedTimeSeriesCount} basic)";
        }

        private List<LocationInfo> GetResolvedLocations()
        {
            var locations = new List<LocationInfo>();

            foreach (var location in Context.LocationsToDelete)
            {
                var resolvedLocations = ResolveLocationInfo(location);

                if (!resolvedLocations.Any())
                    Log.Warn($"Location '{location}' does not exist.");

                locations.AddRange(resolvedLocations);
            }

            return locations;
        }

        private int DeleteLocation(LocationInfo locationInfo)
        {
            ++InspectedLocations;

            if (!ConfirmLocationDeletion(locationInfo))
            {
                return 0;
            }

            Log.Info($"Deleting location '{locationInfo.Identifier}' ...");
            var deletedLocation = DeleteLocationByLocationInfo(locationInfo);
            Log.Info($"Deleted location '{locationInfo.Identifier}' successfully.");

            if (Context.RecreateLocations)
            {
                RecreateLocation(deletedLocation);
            }

            return 1;
        }

        private bool ConfirmLocationDeletion(LocationInfo locationInfo)
        {
            var locationSummary = GetLocationSummary(locationInfo);

            return ConfirmAction(
                $"deletion of location '{locationInfo.Identifier}'",
                $"delete '{locationInfo.Identifier}'",
                () => locationSummary,
                $"the identifier of the location",
                locationInfo.Identifier);
        }

        private bool ConfirmAction(
            string operationDescription,
            string confirmationPrompt,
            Func<string> summarizeAction,
            string confirmationDescription,
            string confirmationResponse)
        {
            var summary = summarizeAction();

            if (Context.DryRun)
            {
                Log.Info($"DryRun: {summary}");
                return false;
            }

            if (Context.SkipConfirmation)
            {
                Log.Warn($"Auto-confirming {operationDescription}");
                return true;
            }

            Log.Warn(summary);
            Log.Warn($"Are you sure you want to {confirmationPrompt}?");
            Log.Warn($"Type {confirmationDescription} to confirm deletion.");

            var response = Console.ReadLine()?.Trim();

            var shouldDelete = confirmationResponse.Equals(response, StringComparison.InvariantCultureIgnoreCase);

            if (!shouldDelete)
            {
                Log.Info($"Skipped {operationDescription}");
            }

            return shouldDelete;
        }

        private void RecreateLocation(Location location)
        {
            var newLocation = Client.Provisioning.Post(new PostLocation
            {
                LocationIdentifier = location.Identifier,
                LocationName = location.LocationName,
                Description = location.Description,
                LocationType = location.LocationType,
                LocationPath = location.LocationPath,
                UtcOffset = location.UtcOffset,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Elevation = location.Elevation,
                ElevationUnits = location.ElevationUnits,
                ExtendedAttributeValues = location.ExtendedAttributeValues
            });

            Log.Info($"Re-created '{newLocation.Identifier}' ({newLocation.UniqueId:N}).");
        }

        private IServiceClient _processor;
        private IServiceClient _siteVisit;
        private IServiceClient _publishForLongOperations;

        private void RegisterPrivateClients()
        {
            _processor = Client.RegisterCustomClient(PrivateApis.Processor.Root.Endpoint);
            _siteVisit = Client.RegisterCustomClient(PrivateApis.SiteVisit.Root.Endpoint);

            _publishForLongOperations = Client.CloneAuthenticatedClient(Client.Publish);
            var publishClone = (JsonServiceClient) _publishForLongOperations;
            publishClone.Timeout = TimeSpan.FromMinutes(10);
        }

        private void AdaptToSpecificVersion()
        {
            if (Is3X())
            {
                AdaptTo3X();
            }
            else
            {
                AdaptToNg();
            }
        }

        private bool Is3X()
        {
            return Client.ServerVersion.IsLessThan(Publish3x.First.Version);
        }

        public class LocationInfo
        {
            public string Identifier { get; set; }
            public string LocationName { get; set; }
            public Guid? UniqueId { get; set; }
        }

        private Func<string,List<LocationInfo>> ResolveLocationInfo { get; set; }
        private Func<LocationInfo,Location> DeleteLocationByLocationInfo { get; set; }
        private Func<LocationInfo,string> GetLocationSummary { get; set; }

        private void AdaptTo3X()
        {
            ResolveLocationInfo = ResolveLocationInfo3X;
            DeleteLocationByLocationInfo = DeleteLocation3X;
            GetLocationSummary = GetLocationSummary3X;
        }

        private void AdaptToNg()
        {
            ResolveLocationInfo = ResolveLocationInfoNg;
            DeleteLocationByLocationInfo = DeleteLocationNg;
            GetLocationSummary = GetLocationSummaryNg;
        }

        private List<LocationInfo> ResolveLocationInfo3X(string locationIdentifier)
        {
            var locationDescriptions = _publishForLongOperations
                .Get(new Publish3x.LocationDescriptionListServiceRequest
                {
                    LocationIdentifier = locationIdentifier
                })
                .LocationDescriptions;

            return locationDescriptions
                .Select(location => new LocationInfo
                {
                    Identifier = location.Identifier,
                    LocationName = location.Name
                })
                .ToList();
        }

        private List<LocationInfo> ResolveLocationInfoNg(string locationIdentifierOrGuid)
        {
            if (Guid.TryParse(locationIdentifierOrGuid, out var uniqueId))
            {
                return ResolveLocationInfoByUniqueId(uniqueId);
            }

            return _publishForLongOperations
                .Get(new LocationDescriptionListServiceRequest {LocationIdentifier = locationIdentifierOrGuid})
                .LocationDescriptions
                .Select(l => new LocationInfo
                {
                    Identifier = l.Identifier,
                    LocationName = l.Name,
                    UniqueId = l.UniqueId
                })
                .ToList();
        }

        private List<LocationInfo> ResolveLocationInfoByUniqueId(Guid uniqueId)
        {
            try
            {
                var location = Client.Provisioning.Get(new GetLocation {LocationUniqueId = uniqueId});

                return new List<LocationInfo>
                {
                    new LocationInfo
                    {
                        Identifier = location.Identifier,
                        LocationName = location.LocationName
                    }
                };
            }
            catch (WebServiceException)
            {
                Log.Warn($"Location uniqueId={uniqueId:N} is not valid");

                return new List<LocationInfo>();
            }
        }

        private Location DeleteLocation3X(LocationInfo locationInfo)
        {
            if (Context.RecreateLocations)
                throw new ExpectedException($"Locations cannot be auto re-created on AQTS {Client.ServerVersion}. Stopping before deleting anything.");

            var location = GetSiteVisitLocation(locationInfo);

            using (var soapClient = LegacyServiceClient.Create(Context.Server, Context.Username, Context.Password))
            {
                var locationsDeleted = soapClient.DeleteLocationAndAllContentById(location.Id, preventContentDeletion:false);

                if (locationsDeleted != 1)
                    throw new ExpectedException($"Unable to delete location '{locationInfo.Identifier}'");
            }

            // We can't recreate a location in 3.X, so no need to return a provisioning object
            return null;
        }

        private SearchLocation GetSiteVisitLocation(LocationInfo locationInfo)
        {
            var searchResults = _siteVisit.Get(new GetSearchLocations {SearchText = locationInfo.Identifier});

            if (searchResults.LimitExceeded)
                throw new ExpectedException($"Cannot resolve location ID for identifier='{locationInfo.Identifier}'. LimitExceeded=true. Results.Count={searchResults.Results.Count}");

            var location = searchResults.Results
                .SingleOrDefault(l => l.Identifier == locationInfo.Identifier && l.Name == locationInfo.LocationName);

            if (location == null)
                throw new ExpectedException($"Cannot resolve locationID for unknown identifier='{locationInfo.Identifier}', even with Results.Count={searchResults.Results.Count}");

            return location;
        }

        private Location DeleteLocationNg(LocationInfo locationInfo)
        {
            if (!locationInfo.UniqueId.HasValue)
                throw new ExpectedException($"No uniqueID found for location '{locationInfo.Identifier}'");

            var location = Client.Provisioning.Get(new GetLocation {LocationUniqueId = locationInfo.UniqueId.Value});

            _processor.Delete(new DeleteMigrationLocationByIdentifier {LocationIdentifier = locationInfo.Identifier});

            return location;
        }

        private string GetLocationSummary3X(LocationInfo location)
        {
            var timeSeries = Client.Publish
                .Get(new Publish3x.TimeSeriesDescriptionServiceRequest {LocationIdentifier = location.Identifier})
                .TimeSeriesDescriptions;

            var derivedTimeSeriesCount = timeSeries
                .Count(ts => !BasicTimeSeriesTypes.Contains(ts.TimeSeriesType));

            var ratingModels = Client.Publish
                .Get(new Publish3x.RatingModelDescriptionListServiceRequest {LocationIdentifier = location.Identifier})
                .RatingModelDescriptions;

            var fieldVisits = Client.Publish
                .Get(new Publish3x.FieldVisitDescriptionListServiceRequest {LocationIdentifier = location.Identifier})
                .FieldVisitDescriptions;

            var locationData = Client.Publish
                .Get(new Publish3x.LocationDataServiceRequest {LocationIdentifier = location.Identifier});

            InspectedTimeSeries += timeSeries.Count;
            InspectedDerivedTimeSeries += derivedTimeSeriesCount;
            InspectedRatingModels += ratingModels.Count;
            InspectedFieldVisits += fieldVisits.Count;

            var inventorySummary = FriendlyList(
                TimeSeriesInventory(timeSeries.Count, derivedTimeSeriesCount),
                "rating model".ToQuantity(ratingModels.Count),
                "field visit".ToQuantity(fieldVisits.Count));

            return $"{location.Identifier} - {locationData.LocationName} has {inventorySummary}.";
        }

        private static readonly HashSet<Publish3x.AtomType> BasicTimeSeriesTypes = new HashSet<Publish3x.AtomType>
        {
            Publish3x.AtomType.TimeSeries_Basic,
            Publish3x.AtomType.TimeSeries_External,
            Publish3x.AtomType.TimeSeries_ProcessorBasic,
        };

        private string GetLocationSummaryNg(LocationInfo location)
        {
            var timeSeries = Client.Publish
                .Get(new TimeSeriesDescriptionServiceRequest {LocationIdentifier = location.Identifier})
                .TimeSeriesDescriptions;

            var derivedTimeSeriesCount = timeSeries
                .Count(ts => ts.TimeSeriesType == "ProcessorDerived");

            var thresholds = timeSeries
                .SelectMany(ts => ts.Thresholds)
                .ToList();

            var ratingModels = Client.Publish
                .Get(new RatingModelDescriptionListServiceRequest {LocationIdentifier = location.Identifier})
                .RatingModelDescriptions;

            var fieldVisits = Client.Publish
                .Get(new FieldVisitDescriptionListServiceRequest {LocationIdentifier = location.Identifier})
                .FieldVisitDescriptions;

            var sensorsAndGauges = Client.Publish
                .Get(new SensorsAndGaugesServiceRequest {LocationIdentifier = location.Identifier})
                .MonitoringMethods;

            var locationData = Client.Publish
                .Get(new LocationDataServiceRequest 
                {
                    LocationIdentifier = location.Identifier,
                    IncludeLocationAttachments = true
                });

            var attachments = locationData
                .Attachments;

            InspectedTimeSeries += timeSeries.Count;
            InspectedDerivedTimeSeries += derivedTimeSeriesCount;
            InspectedThresholds += thresholds.Count;
            InspectedRatingModels += ratingModels.Count;
            InspectedFieldVisits += fieldVisits.Count;
            InspectedSensors += sensorsAndGauges.Count;
            InspectedAttachments += attachments.Count;

            var inventorySummary = FriendlyList(
                TimeSeriesInventory(timeSeries.Count, derivedTimeSeriesCount),
                "threshold".ToQuantity(thresholds.Count),
                "rating model".ToQuantity(ratingModels.Count),
                "field visit".ToQuantity(fieldVisits.Count),
                "sensor".ToQuantity(sensorsAndGauges.Count),
                "attachment".ToQuantity(attachments.Count));

            return $"{location.Identifier} - {locationData.LocationName} has {inventorySummary}.";
        }
    }
}
