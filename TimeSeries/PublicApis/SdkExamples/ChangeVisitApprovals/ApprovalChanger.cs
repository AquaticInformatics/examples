using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using ChangeVisitApprovals.PrivateApis.SiteVisit;
using Humanizer;
using ServiceStack;
using ServiceStack.Logging;
using ApprovalLevel = ChangeVisitApprovals.PrivateApis.SiteVisit.ApprovalLevel;

namespace ChangeVisitApprovals
{
    public class ApprovalChanger
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }
        private IAquariusClient Client { get; set; }
        private List<LocationInfo> ResolvedLocations { get; set; }
        private int InspectedFieldVisits { get; set; }

        public void Run()
        {
            Log.Info($"Connecting {GetExecutingFileVersion()} to {Context.Server} ...");

            using (Client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} (v{Client.ServerVersion})");

                RegisterPrivateClients();

                GetTargetApprovalLevel();

                ResolvedLocations = GetResolvedLocations();

                ChangeFieldVisitApprovals();
            }
        }

        private static string GetExecutingFileVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // ReSharper disable once PossibleNullReferenceException
            return $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} v{fileVersionInfo.FileVersion}";
        }

        private IServiceClient _siteVisit;
        private IServiceClient _publishForLongOperations;

        private void RegisterPrivateClients()
        {
            _siteVisit = Client.RegisterCustomClient(Root.Endpoint);

            _publishForLongOperations = Client.CloneAuthenticatedClient(Client.Publish);
            var publishClone = (JsonServiceClient)_publishForLongOperations;
            publishClone.Timeout = TimeSpan.FromMinutes(10);
        }

        private void GetTargetApprovalLevel()
        {
            var approvalLevels = Client.Publish.Get(new ApprovalListServiceRequest())
                .Approvals;

            if (Context.ApprovalLevel.HasValue)
            {
                var approvalLevelByValue = approvalLevels
                    .SingleOrDefault(a => int.Parse(a.Identifier) == Context.ApprovalLevel.Value);

                if (approvalLevelByValue != null)
                    return;

                throw new ExpectedException($"{Context.ApprovalLevel} is not a valid approval level. Must be one of {string.Join(", ", approvalLevels.Select(a => a.Identifier))}");
            }

            var approvalLevelByName = approvalLevels
                .SingleOrDefault(a => a.DisplayName.Equals(Context.ApprovalName, StringComparison.InvariantCultureIgnoreCase));

            if (approvalLevelByName != null)
            {
                Context.ApprovalLevel = int.Parse(approvalLevelByName.Identifier);

                // Now we can just focus on matching by level
                return;
            }

            throw new ExpectedException($"{Context.ApprovalName} is not a valid approval level name. Must be one of {string.Join(", ", approvalLevels.Select(a => a.DisplayName))}");
        }

        private void ChangeFieldVisitApprovals()
        {
            if (!ResolvedLocations.Any())
            {
                // Change field visits from all locations
                Context.Locations = new List<string>{"*"};

                ResolvedLocations = GetResolvedLocations()
                    .OrderBy(l => l.Identifier)
                    .ToList();
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

            var changedVisitCount = 0;

            foreach (var locationInfo in ResolvedLocations)
            {
                changedVisitCount += ChangeVisitsAtLocation(locationInfo);
            }

            if (Context.DryRun)
                Log.Info($"Dry run completed. {"field visit".ToQuantity(InspectedFieldVisits)} would have been changed from {locationQuantity}.");
            else
                Log.Info($"Changed {"field visit".ToQuantity(changedVisitCount)} from {locationQuantity}.");
        }

        private int ChangeVisitsAtLocation(LocationInfo locationInfo)
        {
            var siteVisitLocation = GetSiteVisitLocation(locationInfo);

            var targetApprovalLevel = GetTargetApprovalLevel(siteVisitLocation.Id);

            var visits = _siteVisit.Get(new GetLocationVisits
                {
                    Id = siteVisitLocation.Id,
                    StartTime = Context.VisitsAfter?.UtcDateTime,
                    EndTime = Context.VisitsBefore?.UtcDateTime
                })
                .Where(v => v.ApprovalLevelId != targetApprovalLevel.Id)
                .OrderBy(v => v.StartDate)
                .ToList();

            if (!visits.Any())
            {
                Log.Info($"No field visits to change in location '{locationInfo.Identifier}'.");
                return 0;
            }

            InspectedFieldVisits += visits.Count;
            var visitQuantity = $"{"field visit".ToQuantity(visits.Count)} at location '{locationInfo.Identifier}'";
            var startDate = visits.First().StartDate;
            var endDate = visits.Last().EndDate;
            var visitSummary = $"{visitQuantity} from {startDate} to {endDate}";

            if (!ConfirmAction(
                $"change of {visitQuantity}",
                $"change {visitQuantity}",
                () => visitSummary,
                $"the identifier of the location",
                locationInfo.Identifier))
            {
                return 0;
            }

            foreach (var visit in visits)
            {
                _siteVisit.Put(new PutVisitApprovalLevel {Id = visit.Id, ApprovalLevelId = targetApprovalLevel.Id});
                Log.Info($"Changed '{locationInfo.Identifier}' visit Start={visit.StartDate} End={visit.EndDate} to approval level {targetApprovalLevel.Level} ({targetApprovalLevel.Name})");
            }

            Log.Info($"Changed approval level on {visitSummary} successfully.");

            return visits.Count;
        }

        private List<LocationInfo> GetResolvedLocations()
        {
            var locations = new List<LocationInfo>();

            foreach (var location in Context.Locations)
            {
                var resolvedLocations = ResolveLocationInfo(location);

                if (!resolvedLocations.Any())
                    Log.Warn($"Location '{location}' does not exist.");

                locations.AddRange(resolvedLocations);
            }

            return locations;
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
            Log.Warn($"Type {confirmationDescription} to confirm the operation.");

            var response = Console.ReadLine()?.Trim();

            var confirmed = confirmationResponse.Equals(response, StringComparison.InvariantCultureIgnoreCase);

            if (!confirmed)
            {
                Log.Info($"Skipped {operationDescription}");
            }

            return confirmed;
        }

        public class LocationInfo
        {
            public string Identifier { get; set; }
            public string LocationName { get; set; }
            public Guid? UniqueId { get; set; }
        }

        private List<LocationInfo> ResolveLocationInfo(string locationIdentifierOrGuid)
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

        private ApprovalLevel GetTargetApprovalLevel(long locationId)
        {
            var response = _siteVisit.Get(new GetLocationApprovalLevels {Id = locationId});

            return response
                .ApprovalLevels
                .Single(a => a.Level == Context.ApprovalLevel);
        }
    }
}
