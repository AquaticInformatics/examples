using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using log4net;
using ServiceStack;
using TimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesDescription;

namespace SosExporter
{
    public class Exporter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private IAquariusClient Aquarius { get; set; }
        private ISosClient Sos { get; set; }
        private SyncStatus SyncStatus { get; set; }
        private TimeSeriesPointFilter TimeSeriesPointFilter { get; set; }
        private long ExportedPointCount { get; set; }
        private int ExportedTimeSeriesCount { get; set; }

        public void Run()
        {
            Log.Info($"{GetProgramVersion()} connecting to {Context.Config.AquariusServer} ...");

            using (Aquarius = AquariusClient.CreateConnectedClient(Context.Config.AquariusServer, Context.Config.AquariusUsername, Context.Config.AquariusPassword))
            {
                Log.Info($"Connected to {Context.Config.AquariusServer} (v{Aquarius.ServerVersion}) as {Context.Config.AquariusUsername}");

                if (Aquarius.ServerVersion.IsLessThan(MinimumVersion))
                    throw new ExpectedException($"This utility requires AQTS v{MinimumVersion} or greater.");

                var stopwatch = Stopwatch.StartNew();

                RunOnce();

                Log.Info($"Successfully exported {ExportedPointCount} points from {ExportedTimeSeriesCount} time-series in {stopwatch.Elapsed.Humanize()}");
            }
        }

        private static readonly AquariusServerVersion MinimumVersion = AquariusServerVersion.Create("17.2");

        private static string GetProgramVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // ReSharper disable once PossibleNullReferenceException
            return $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} v{fileVersionInfo.FileVersion}";
        }

        private void LogDryRun(string message)
        {
            Log.Warn($"Dry-run: {message}");
        }

        private void RunOnce()
        {
            SyncStatus = new SyncStatus(Aquarius) {Context = Context};
            TimeSeriesPointFilter = new TimeSeriesPointFilter {Context = Context};

            ValidateFilters();

            var request = CreateFilterRequest();

            if (Context.ForceResync)
            {
                Log.Warn("Forcing a full time-series resync.");
                request.ChangesSinceToken = null;
            }
            else if (Context.ChangesSince.HasValue)
            {
                Log.Warn($"Overriding current ChangesSinceToken='{request.ChangesSinceToken:O}' with '{Context.ChangesSince:O}'");
                request.ChangesSinceToken = Context.ChangesSince.Value.UtcDateTime;
            }

            Log.Info($"Checking {GetFilterSummary(request)} ...");

            var stopwatch = Stopwatch.StartNew();

            var response = Aquarius.Publish.Get(request);

            if (response.TokenExpired ?? false)
            {
                if (Context.NeverResync)
                {
                    Log.Warn("Skipping a recommended resync.");
                }
                else
                {
                    Log.Warn($"The ChangesSinceToken of {request.ChangesSinceToken:O} has expired. Forcing a full resync. You may need to run the exporter more frequently.");
                    request.ChangesSinceToken = null;

                    response = Aquarius.Publish.Get(request);
                }
            }

            var bootstrapToken = response.ResponseTime
                .Subtract(stopwatch.Elapsed)
                .Subtract(TimeSpan.FromMinutes(1))
                .UtcDateTime;

            var nextChangesSinceToken = response.NextToken ?? bootstrapToken;

            Log.Info($"Fetching descriptions of {response.TimeSeriesUniqueIds.Count} changed time-series ...");

            var timeSeriesDescriptions = FetchChangedTimeSeriesDescriptions(
                response.TimeSeriesUniqueIds
                    .Select(ts => ts.UniqueId)
                    .ToList());

            Log.Info($"Connecting to {Context.Config.SosServer} ...");

            using (Sos = SosClient.CreateConnectedClient(Context.Config.SosServer, Context.Config.SosUsername, Context.Config.SosPassword))
            {
                Log.Info($"Connected to {Context.Config.SosServer} as {Context.Config.SosUsername}");

                ExportToSos(request, response, timeSeriesDescriptions);
            }

            SyncStatus.SaveConfiguration(nextChangesSinceToken);
        }

        private void ValidateFilters()
        {
            ValidateApprovalFilters();
            ValidateGradeFilters();
            ValidateQualifierFilters();
        }

        private void ValidateApprovalFilters()
        {
            if (!Context.Config.Approvals.Any()) return;

            Log.Info("Fetching approval configuration ...");
            var approvals = Aquarius.Publish.Get(new ApprovalListServiceRequest()).Approvals;

            foreach (var approvalFilter in Context.Config.Approvals)
            {
                var approvalMetadata = approvals.SingleOrDefault(a =>
                    a.DisplayName.Equals(approvalFilter.Text, StringComparison.InvariantCultureIgnoreCase)
                    || a.Identifier.Equals(approvalFilter.Text, StringComparison.InvariantCultureIgnoreCase));

                if (approvalMetadata == null)
                    throw new ExpectedException($"Unknown approval '{approvalFilter.Text}'");

                approvalFilter.Text = approvalMetadata.DisplayName;
                approvalFilter.ApprovalLevel = int.Parse(approvalMetadata.Identifier);
            }
        }

        private void ValidateGradeFilters()
        {
            if (!Context.Config.Grades.Any()) return;

            Log.Info("Fetching grade configuration ...");
            var grades = Aquarius.Publish.Get(new GradeListServiceRequest()).Grades;

            foreach (var gradeFilter in Context.Config.Grades)
            {
                var gradeMetadata = grades.SingleOrDefault(g =>
                    g.DisplayName.Equals(gradeFilter.Text, StringComparison.InvariantCultureIgnoreCase)
                    || g.Identifier.Equals(gradeFilter.Text, StringComparison.InvariantCultureIgnoreCase));

                if (gradeMetadata == null)
                    throw new ExpectedException($"Unknown grade '{gradeFilter.Text}'");

                gradeFilter.Text = gradeMetadata.DisplayName;
                gradeFilter.GradeCode = int.Parse(gradeMetadata.Identifier);
            }
        }

        private void ValidateQualifierFilters()
        {
            if (!Context.Config.Qualifiers.Any()) return;

            Log.Info("Fetching qualifier configuration ...");
            var qualifiers = Aquarius.Publish.Get(new QualifierListServiceRequest()).Qualifiers;

            foreach (var qualifierFilter in Context.Config.Qualifiers)
            {
                var qualifierMetadata = qualifiers.SingleOrDefault(q =>
                    q.Identifier.Equals(qualifierFilter.Text, StringComparison.InvariantCultureIgnoreCase)
                    || q.Code.Equals(qualifierFilter.Text, StringComparison.InvariantCultureIgnoreCase));

                if (qualifierMetadata == null)
                    throw new ExpectedException($"Unknown qualifier '{qualifierFilter.Text}'");

                qualifierFilter.Text = qualifierMetadata.Identifier;
            }
        }

        private TimeSeriesUniqueIdListServiceRequest CreateFilterRequest()
        {
            var locationIdentifier = Context.Config.LocationIdentifier;

            if (!string.IsNullOrEmpty(locationIdentifier))
            {
                var locationDescription = Aquarius.Publish
                    .Get(new LocationDescriptionListServiceRequest { LocationIdentifier = locationIdentifier })
                    .LocationDescriptions
                    .SingleOrDefault();

                if (locationDescription == null)
                    throw new ExpectedException($"Location '{locationIdentifier}' does not exist.");

                locationIdentifier = locationDescription.Identifier;
            }

            return new TimeSeriesUniqueIdListServiceRequest
            {
                ChangesSinceToken = SyncStatus.GetLastChangesSinceToken(),
                LocationIdentifier = locationIdentifier,
                ChangeEventType = Context.Config.ChangeEventType?.ToString(),
                Publish = Context.Config.Publish,
                Parameter = Context.Config.Parameter,
                ComputationIdentifier = Context.Config.ComputationIdentifier,
                ComputationPeriodIdentifier = Context.Config.ComputationPeriodIdentifier,
                ExtendedFilters = Context.Config.ExtendedFilters.Any() ? Context.Config.ExtendedFilters : null,
            };
        }

        private string GetFilterSummary(TimeSeriesUniqueIdListServiceRequest request)
        {
            var sb = new StringBuilder();

            sb.Append(string.IsNullOrEmpty(request.LocationIdentifier)
                ? "all locations"
                : $"location '{request.LocationIdentifier}'");

            var filters = new List<string>();

            if (request.Publish.HasValue)
            {
                filters.Add($"Publish={request.Publish}");
            }

            if (!string.IsNullOrEmpty(request.Parameter))
            {
                filters.Add($"Parameter={request.Parameter}");
            }

            if (!string.IsNullOrEmpty(request.ComputationIdentifier))
            {
                filters.Add($"ComputationIdentifier={request.ComputationIdentifier}");
            }

            if (!string.IsNullOrEmpty(request.ComputationPeriodIdentifier))
            {
                filters.Add($"ComputationPeriodIdentifier={request.ComputationPeriodIdentifier}");
            }

            if (!string.IsNullOrEmpty(request.ChangeEventType))
            {
                filters.Add($"ChangeEventType={request.ChangeEventType}");
            }

            if (request.ExtendedFilters != null && request.ExtendedFilters.Any())
            {
                filters.Add($"ExtendedFilters={string.Join(", ", request.ExtendedFilters.Select(f => $"{f.FilterName}={f.FilterValue}"))}");
            }

            if (filters.Any())
            {
                sb.Append($" with {string.Join(" and ", filters)}");
            }

            sb.Append(" for time-series");

            if (request.ChangesSinceToken.HasValue)
            {
                sb.Append($" change since {request.ChangesSinceToken:O}");
            }

            return sb.ToString();
        }


        private List<TimeSeriesDescription> FetchChangedTimeSeriesDescriptions(List<Guid> timeSeriesUniqueIdsToFetch)
        {
            var timeSeriesDescriptions = new List<TimeSeriesDescription>();

            using (var batchClient = CreatePublishClientWithPostMethodOverride())
            {
                while (timeSeriesUniqueIdsToFetch.Any())
                {
                    const int batchSize = 400;

                    var batchList = timeSeriesUniqueIdsToFetch.Take(batchSize).ToList();
                    timeSeriesUniqueIdsToFetch = timeSeriesUniqueIdsToFetch.Skip(batchSize).ToList();

                    var request = new TimeSeriesDescriptionListByUniqueIdServiceRequest();

                    // We need to resolve the URL without any unique IDs on the GET command line
                    var requestUrl = RemoveQueryFromUrl(request.ToGetUrl());

                    request.TimeSeriesUniqueIds = batchList;

                    var batchResponse =
                        batchClient.Send<TimeSeriesDescriptionListByUniqueIdServiceResponse>(HttpMethods.Post,
                            requestUrl, request);

                    timeSeriesDescriptions.AddRange(batchResponse.TimeSeriesDescriptions);
                }
            }

            return timeSeriesDescriptions
                .OrderBy(ts => ts.LocationIdentifier)
                .ThenBy(ts => ts.Identifier)
                .ToList();
        }

        private JsonServiceClient CreatePublishClientWithPostMethodOverride()
        {
            return Aquarius.CloneAuthenticatedClientWithOverrideMethod(Aquarius.Publish, HttpMethods.Get) as JsonServiceClient;
        }

        private static string RemoveQueryFromUrl(string url)
        {
            var queryIndex = url.IndexOf("?", StringComparison.InvariantCulture);

            if (queryIndex < 0)
                return url;

            return url.Substring(0, queryIndex);
        }

        private List<TimeSeriesDescription> FilterTimeSeriesDescriptions(List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            if (!Context.Config.TimeSeries.Any())
                return timeSeriesDescriptions;

            var timeSeriesFilter = new Filter<TimeSeriesFilter>(Context.Config.TimeSeries);

            var results = new List<TimeSeriesDescription>();

            foreach (var timeSeriesDescription in timeSeriesDescriptions)
            {
                if (timeSeriesFilter.IsFiltered(f => f.Regex.IsMatch(timeSeriesDescription.Identifier)))
                    continue;

                results.Add(timeSeriesDescription);
            }

            return results;
        }

        private void ExportToSos(
            TimeSeriesUniqueIdListServiceRequest request,
            TimeSeriesUniqueIdListServiceResponse response,
            List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            Sos.MaximumPointsPerObservation = Context.MaximumPointsPerObservation;

            var filteredTimeSeriesDescriptions = FilterTimeSeriesDescriptions(timeSeriesDescriptions);

            Log.Info($"Exporting {filteredTimeSeriesDescriptions.Count} time-series ...");

            var clearExportedData = !request.ChangesSinceToken.HasValue;

            if (clearExportedData)
            {
                ClearExportedData();
            }

            foreach (var timeSeriesDescription in filteredTimeSeriesDescriptions)
            {
                ExportTimeSeries(
                    clearExportedData,
                    response.TimeSeriesUniqueIds.Single(t => t.UniqueId == timeSeriesDescription.UniqueId),
                    timeSeriesDescription);
            }
        }

        private void ClearExportedData()
        {
            if (Context.DryRun)
            {
                LogDryRun("Would have cleared the SOS database of all existing data.");
                return;
            }

            Sos.ClearDatasource();
            Sos.DeleteDeletedObservations();
        }

        private void ExportTimeSeries(
            bool clearExportedData,
            TimeSeriesUniqueIds detectedChange,
            TimeSeriesDescription timeSeriesDescription)
        {
            Log.Info($"Fetching changes from '{timeSeriesDescription.Identifier}' FirstPointChanged={detectedChange.FirstPointChanged:O} HasAttributeChanged={detectedChange.HasAttributeChange} ...");

            var locationInfo = GetLocationInfo(timeSeriesDescription.LocationIdentifier);

            var period = GetTimeSeriesPeriod(timeSeriesDescription);

            var dataRequest = new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesDescription.UniqueId,
                QueryFrom = detectedChange.FirstPointChanged,
                ApplyRounding = true,
            };

            var timeSeries = Aquarius.Publish.Get(dataRequest);

            var existingSensor = Sos.FindExistingSensor(timeSeries);

            var deleteExistingSensor = !clearExportedData && existingSensor?.PhenomenonTime.Last() >= detectedChange.FirstPointChanged;

            if (existingSensor == null || deleteExistingSensor || clearExportedData)
            {
                // We may need to fetch more points than just the latest changed points
                var daysToExtract = Context.Config.MaximumPointDays[period];

                var originalQueryFrom = dataRequest.QueryFrom;

                if (daysToExtract <= 0 || period == ComputationPeriod.Unknown)
                {
                    // Fetch the whole signal
                    dataRequest.QueryFrom = null;
                }
                else
                {
                    dataRequest.QueryFrom = SubtractTimeSpan(
                        detectedChange.FirstPointChanged ?? DateTimeOffset.UtcNow,
                        TimeSpan.FromDays(daysToExtract));
                }

                if (originalQueryFrom != null && originalQueryFrom != dataRequest.QueryFrom)
                {
                    Log.Info($"Fetching more than changed points from '{timeSeriesDescription.Identifier}' with QueryFrom={dataRequest.QueryFrom:O} ...");

                    timeSeries = Aquarius.Publish.Get(dataRequest);
                }

                if (period == ComputationPeriod.Unknown)
                {
                    period = ComputationPeriodEstimator.InferPeriodFromRecentPoints(timeSeries);
                    daysToExtract = Context.Config.MaximumPointDays[period];
                }

                if (daysToExtract > 0 && timeSeries.Points.Any())
                {
                    var earliestDayToUpload = SubtractTimeSpan(
                        timeSeries.Points.Last().Timestamp.DateTimeOffset,
                        TimeSpan.FromDays(daysToExtract));

                    var remainingPoints = timeSeries.Points
                        .Where(p => p.Timestamp.DateTimeOffset >= earliestDayToUpload)
                        .ToList();

                    var trimmedPointCount = timeSeries.NumPoints - remainingPoints.Count;

                    Log.Info($"Trimming '{timeSeriesDescription.Identifier}' {trimmedPointCount} points before {earliestDayToUpload:O} with {remainingPoints.Count} points remaining with Frequency={period}");

                    timeSeries.Points = remainingPoints;
                    timeSeries.NumPoints = timeSeries.Points.Count;
                }
            }

            TimeSeriesPointFilter.FilterTimeSeriesPoints(timeSeries);

            var createSensor = existingSensor == null || deleteExistingSensor || clearExportedData;
            var assignedOffering = existingSensor?.Identifier;

            var exportSummary = $"{timeSeries.NumPoints} points [{timeSeries.Points.FirstOrDefault()?.Timestamp.DateTimeOffset:O} to {timeSeries.Points.LastOrDefault()?.Timestamp.DateTimeOffset:O}] from '{timeSeriesDescription.Identifier}' with Frequency={period}";

            ExportedTimeSeriesCount += 1;
            ExportedPointCount += timeSeries.NumPoints ?? 0;

            if (Context.DryRun)
            {
                if (deleteExistingSensor)
                    LogDryRun($"Would delete existing sensor '{existingSensor.Identifier}'");

                if (createSensor)
                    LogDryRun($"Would create new sensor for '{timeSeriesDescription.Identifier}'");

                LogDryRun($"Would export {exportSummary}.");
                return;
            }

            Log.Info($"Exporting {exportSummary} ...");

            if (deleteExistingSensor)
            {
                Sos.DeleteSensor(timeSeries);
                Sos.DeleteDeletedObservations();
            }

            if (createSensor)
            {
                var sensor = Sos.InsertSensor(timeSeries);

                assignedOffering = sensor.AssignedOffering;
            }

            Sos.InsertObservation(assignedOffering, locationInfo.LocationData, locationInfo.LocationDescription, timeSeries, timeSeriesDescription);
        }

        private (LocationDescription LocationDescription, LocationDataServiceResponse LocationData) GetLocationInfo(string locationIdentifier)
        {
            if (LocationInfoCache.TryGetValue(locationIdentifier, out var locationInfo))
                return locationInfo;

            var locationDescription = Aquarius.Publish.Get(new LocationDescriptionListServiceRequest
            {
                LocationIdentifier = locationIdentifier
            }).LocationDescriptions.Single();

            var locationData = Aquarius.Publish.Get(new LocationDataServiceRequest
            {
                LocationIdentifier = locationIdentifier
            });

            locationInfo = (locationDescription, locationData);

            LocationInfoCache.Add(locationIdentifier, locationInfo);

            return locationInfo;
        }

        private
            Dictionary<string, (LocationDescription LocationDescription, LocationDataServiceResponse LocationData)>
            LocationInfoCache { get; } =
                new Dictionary<string, (LocationDescription LocationDescription, LocationDataServiceResponse LocationData)>();

        private static DateTimeOffset SubtractTimeSpan(DateTimeOffset dateTimeOffset, TimeSpan timeSpan)
        {
            return dateTimeOffset.Subtract(DateTimeOffset.MinValue) <= timeSpan
                ? DateTimeOffset.MinValue
                : dateTimeOffset.Subtract(timeSpan);
        }

        private ComputationPeriod GetTimeSeriesPeriod(TimeSeriesDescription timeSeriesDescription)
        {
            if (Enum.TryParse<ComputationPeriod>(timeSeriesDescription.ComputationPeriodIdentifier, true, out var period))
            {
                if (period == ComputationPeriod.WaterYear)
                    period = ComputationPeriod.Annual; // WaterYear and Annual are the same frequency

                if (Context.Config.MaximumPointDays.ContainsKey(period))
                    return period;
            }

            // Otherwise fall back to the "I don't know" setting
            return ComputationPeriod.Unknown;
        }
    }
}
