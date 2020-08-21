using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using log4net;
using ServiceStack;
using SosExporter.Dtos;
using TimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesDescription;
using TimeSeriesChangeEvent = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesUniqueIds;

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
        private TimeSpan MaximumExportDuration { get; set; }

        public void Run()
        {
            Log.Info($"{ExeHelper.ExeNameAndVersion} connecting to {Context.Config.AquariusServer} ...");

            using (Aquarius = CreateConnectedAquariusClient())
            {
                Log.Info($"Connected to {Context.Config.AquariusServer} (v{Aquarius.ServerVersion}) as {Context.Config.AquariusUsername}");

                if (Aquarius.ServerVersion.IsLessThan(MinimumVersion))
                    throw new ExpectedException($"This utility requires AQTS v{MinimumVersion} or greater.");

                var stopwatch = Stopwatch.StartNew();

                RunOnce();

                Log.Info($"Successfully exported {ExportedPointCount} points from {ExportedTimeSeriesCount} time-series in {stopwatch.Elapsed.Humanize(2)}");
            }
        }

        private static readonly AquariusServerVersion MinimumVersion = AquariusServerVersion.Create("17.2");

        private IAquariusClient CreateConnectedAquariusClient()
        {
            var client = AquariusClient.CreateConnectedClient(
                Context.Config.AquariusServer,
                Context.Config.AquariusUsername,
                Context.Config.AquariusPassword);

            foreach (var serviceClient in new[]{client.Publish, client.Provisioning, client.Acquisition})
            {
                if (!(serviceClient is JsonServiceClient jsonClient))
                    continue;

                jsonClient.Timeout = Context.Timeout;
                jsonClient.ReadWriteTimeout = Context.Timeout;
            }

            return client;
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

            MaximumExportDuration = Context.MaximumPollDuration
                                    ?? SyncStatus.GetMaximumChangeEventDuration()
                                        .Subtract(TimeSpan.FromHours(1));

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

            var timeSeriesDescriptions = FetchChangedTimeSeriesDescriptions(response);

            Log.Info($"Connecting to {Context.Config.SosServer} as {Context.Config.SosUsername} ...");

            var clearExportedData = !request.ChangesSinceToken.HasValue;
            request.ChangesSinceToken = nextChangesSinceToken;

            ExportToSos(request, response, timeSeriesDescriptions, clearExportedData);

            SyncStatus.SaveConfiguration(request.ChangesSinceToken.Value);
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

        private List<TimeSeriesDescription> FetchChangedTimeSeriesDescriptions(TimeSeriesUniqueIdListServiceResponse response)
        {
            var timeSeriesUniqueIdsToFetch = response.TimeSeriesUniqueIds
                .Select(ts => ts.UniqueId)
                .ToList();

            Log.Info($"Fetching descriptions of {timeSeriesUniqueIdsToFetch.Count} changed time-series ...");

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
            return FilterTimeSeriesDescriptionsByDescription(
                FilterTimeSeriesDescriptionsByIdentifier(timeSeriesDescriptions));
        }

        private List<TimeSeriesDescription> FilterTimeSeriesDescriptionsByIdentifier(List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            return FilterTimeSeriesDescriptionsByText(
                timeSeriesDescriptions,
                Context.Config.TimeSeries,
                ts => ts.Identifier);
        }

        private List<TimeSeriesDescription> FilterTimeSeriesDescriptionsByDescription(List<TimeSeriesDescription> timeSeriesDescriptions)
        {
            return FilterTimeSeriesDescriptionsByText(
                timeSeriesDescriptions,
                Context.Config.TimeSeriesDescriptions,
                ts => ts.Description);
        }

        private static List<TimeSeriesDescription> FilterTimeSeriesDescriptionsByText(
            List<TimeSeriesDescription> timeSeriesDescriptions,
            List<TimeSeriesFilter> filters,
            Func<TimeSeriesDescription, string> textSelector)
        {
            if (!filters.Any())
                return timeSeriesDescriptions;

            var timeSeriesFilter = new Filter<TimeSeriesFilter>(filters);

            var results = new List<TimeSeriesDescription>();

            foreach (var timeSeriesDescription in timeSeriesDescriptions)
            {
                if (timeSeriesFilter.IsFiltered(f => f.Regex.IsMatch(textSelector(timeSeriesDescription))))
                    continue;

                results.Add(timeSeriesDescription);
            }

            return results;
        }

        private void ExportToSos(
            TimeSeriesUniqueIdListServiceRequest request,
            TimeSeriesUniqueIdListServiceResponse response,
            List<TimeSeriesDescription> timeSeriesDescriptions,
            bool clearExportedData)
        {
            var filteredTimeSeriesDescriptions = FilterTimeSeriesDescriptions(timeSeriesDescriptions);
            var changeEvents = response.TimeSeriesUniqueIds;

            Log.Info($"Exporting {filteredTimeSeriesDescriptions.Count} time-series ...");

            if (clearExportedData)
            {
                ClearExportedData();
            }

            var stopwatch = Stopwatch.StartNew();

            foreach (var timeSeriesDescription in filteredTimeSeriesDescriptions)
            {
                using (Sos = SosClient.CreateConnectedClient(Context))
                {
                    // Create a separate SOS client connection to ensure that the transactions are committed after each export
                    var description = timeSeriesDescription;
                    ExportTimeSeries(
                        clearExportedData,
                        request.ChangesSinceToken,
                        changeEvents.Single(t => t.UniqueId == description.UniqueId),
                        timeSeriesDescription);
                }

                if (stopwatch.Elapsed <= MaximumExportDuration)
                    continue;

                Log.Info($"Maximum export duration has elapsed. Checking {GetFilterSummary(request)} ...");

                stopwatch.Restart();

                FetchNewChanges(request, filteredTimeSeriesDescriptions, changeEvents);
            }
        }

        private void FetchNewChanges(TimeSeriesUniqueIdListServiceRequest request, List<TimeSeriesDescription> timeSeriesToExport, List<TimeSeriesChangeEvent> timeSeriesChangeEvents)
        {
            var response = Aquarius.Publish.Get(request);

            if (response.TokenExpired ?? !response.NextToken.HasValue)
                throw new ExpectedException($"Logic-error: A secondary changes-since response should always have an updated token.");

            request.ChangesSinceToken = response.NextToken;

            var newTimeSeriesDescriptions = FilterTimeSeriesDescriptions(FetchChangedTimeSeriesDescriptions(response));

            if (!newTimeSeriesDescriptions.Any())
                return;

            Log.Info($"Merging {newTimeSeriesDescriptions.Count} changed time-series into the export queue ...");

            timeSeriesToExport.AddRange(newTimeSeriesDescriptions);

            foreach (var newTimeSeriesDescription in newTimeSeriesDescriptions)
            {
                var newEvent = response.TimeSeriesUniqueIds.Single(e => e.UniqueId == newTimeSeriesDescription.UniqueId);

                var existingEvent = timeSeriesChangeEvents.SingleOrDefault(e => e.UniqueId == newEvent.UniqueId);

                if (existingEvent == null)
                {
                    timeSeriesChangeEvents.Add(newEvent);
                    continue;
                }

                MergeTimeSeriesChangeEvent(existingEvent, newEvent);
            }
        }

        private static void MergeTimeSeriesChangeEvent(TimeSeriesChangeEvent existingEvent, TimeSeriesChangeEvent newEvent)
        {
            if (existingEvent.HasAttributeChange.HasValue && newEvent.HasAttributeChange.HasValue)
            {
                existingEvent.HasAttributeChange = existingEvent.HasAttributeChange.Value || newEvent.HasAttributeChange.Value;
            }
            else if (newEvent.HasAttributeChange.HasValue)
            {
                existingEvent.HasAttributeChange = newEvent.HasAttributeChange;
            }

            if (existingEvent.FirstPointChanged.HasValue && newEvent.FirstPointChanged.HasValue)
            {
                if (newEvent.FirstPointChanged < existingEvent.FirstPointChanged)
                {
                    existingEvent.FirstPointChanged = newEvent.FirstPointChanged;
                }
            }
            else if (newEvent.FirstPointChanged.HasValue)
            {
                existingEvent.FirstPointChanged = newEvent.FirstPointChanged;
            }
        }

        private void ClearExportedData()
        {
            if (Context.DryRun)
            {
                LogDryRun("Would have cleared the SOS database of all existing data.");
                return;
            }

            using (var sosClient = SosClient.CreateConnectedClient(Context))
            {
                // Create a separate SOS client connection to ensure that the transactions are committed when the client disconnects
                sosClient.ClearDatasource();
                sosClient.DeleteDeletedObservations();
            }
        }

        private void ExportTimeSeries(bool clearExportedData,
            DateTime? nextChangesSinceToken,
            TimeSeriesChangeEvent detectedChange,
            TimeSeriesDescription timeSeriesDescription)
        {
            var locationInfo = GetLocationInfo(timeSeriesDescription.LocationIdentifier);

            var (exportDuration, exportLabel) = GetExportDuration(timeSeriesDescription);

            var dataRequest = new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesDescription.UniqueId,
                QueryFrom = GetInitialQueryFrom(detectedChange),
                ApplyRounding = Context.ApplyRounding,
            };

            var existingSensor = Sos.FindExistingSensor(timeSeriesDescription);
            var deleteExistingSensor = clearExportedData && existingSensor != null;
            var assignedOffering = existingSensor?.Identifier;

            var lastSensorTime = GetLastSensorTime(existingSensor);

            if (HaveExistingSosPointsChanged(dataRequest, lastSensorTime, detectedChange, timeSeriesDescription))
            {
                Log.Warn($"FirstPointChanged={detectedChange.FirstPointChanged:O} AttributeChange={detectedChange.HasAttributeChange} of '{timeSeriesDescription.Identifier}' precedes LastSensorTime={lastSensorTime:O} of '{existingSensor?.Identifier}'. Forcing delete of existing sensor.");

                // A point has changed before the last known observation, so we'll need to throw out the entire sensor
                deleteExistingSensor = true;

                // We'll also need to fetch more data again
                dataRequest.QueryFrom = null;
            }

            if (dataRequest.QueryFrom == null)
            {
                // Get the full extraction
                var endPoint = dataRequest.QueryTo ?? DateTimeOffset.Now;
                var startOfToday = new DateTimeOffset(endPoint.Year, endPoint.Month, endPoint.Day, 0, 0, 0,
                    timeSeriesDescription.UtcOffsetIsoDuration.ToTimeSpan());

                dataRequest.QueryFrom = startOfToday - exportDuration;
            }

            Log.Info($"Fetching changes from '{timeSeriesDescription.Identifier}' FirstPointChanged={detectedChange.FirstPointChanged:O} HasAttributeChanged={detectedChange.HasAttributeChange} QueryFrom={dataRequest.QueryFrom:O} ...");

            var timeSeries = Aquarius.Publish.Get(dataRequest);

            TrimExcludedPoints(timeSeriesDescription, timeSeries, nextChangesSinceToken);

            var createSensor = existingSensor == null || deleteExistingSensor;

            TimeSeriesPointFilter.FilterTimeSeriesPoints(timeSeries);

            var exportSummary = $"{timeSeries.NumPoints} points [{timeSeries.Points.FirstOrDefault()?.Timestamp.DateTimeOffset:O} to {timeSeries.Points.LastOrDefault()?.Timestamp.DateTimeOffset:O}] from '{timeSeriesDescription.Identifier}' with ExportDuration={exportLabel}";

            ExportedTimeSeriesCount += 1;
            ExportedPointCount += timeSeries.NumPoints ?? 0;

            if (Context.DryRun)
            {
                if (deleteExistingSensor)
                    LogDryRun($"Would delete existing sensor '{existingSensor?.Identifier}'");

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

            Sos.InsertObservation(assignedOffering, locationInfo.LocationData, locationInfo.LocationDescription, timeSeries);
        }

        private static DateTimeOffset? GetInitialQueryFrom(TimeSeriesChangeEvent detectedChange)
        {
            // When a derived time-series is reported as changed, the first point changed is always the beginning of time.
            // Rather than always pull the whole signal (which can be expensive with rounded values), only to trim most points before exporting,
            // just treat a re-derived series event like an initial sync, so we'll "walk backwards" from the current time.
            //
            // If and when partial-re-derivation is implemented, this condition will no longer be triggered.
            return detectedChange.FirstPointChanged == DateTimeOffset.MinValue
                ? null
                : detectedChange.FirstPointChanged;
        }

        private static DateTimeOffset? GetLastSensorTime(SensorInfo sensor)
        {
            return sensor?.PhenomenonTime.LastOrDefault();
        }

        private bool HaveExistingSosPointsChanged(
            TimeSeriesDataCorrectedServiceRequest dataRequest,
            DateTimeOffset? lastSensorTime,
            TimeSeriesChangeEvent detectedChange,
            TimeSeriesDescription timeSeriesDescription)
        {
            if (detectedChange.HasAttributeChange ?? false)
                return true;

            if (!detectedChange.FirstPointChanged.HasValue || !lastSensorTime.HasValue)
                return false;

            if (lastSensorTime < detectedChange.FirstPointChanged)
                return false;

            dataRequest.QueryFrom = lastSensorTime;

            var timeSeriesIdentifier = timeSeriesDescription.Identifier;

            var sosPoints = new Queue<TimeSeriesPoint>(Sos.GetObservations(timeSeriesDescription, detectedChange.FirstPointChanged.Value, lastSensorTime.Value));
            var aqtsPoints = new Queue<TimeSeriesPoint>(Aquarius.Publish.Get(dataRequest).Points);

            var sosCount = sosPoints.Count;
            var aqtsCount = aqtsPoints.Count;

            Log.Info($"Fetched {sosCount} SOS points and {aqtsCount} AQUARIUS points for '{timeSeriesIdentifier}' from {lastSensorTime:O} ...");

            while (sosPoints.Any() || aqtsPoints.Any())
            {
                var sosPoint = sosPoints.FirstOrDefault();
                var aqtsPoint = aqtsPoints.FirstOrDefault();

                if (aqtsPoint == null)
                {
                    Log.Warn($"'{timeSeriesIdentifier}': AQUARIUS now has fewer points than SOS@{sosPoint?.Timestamp.DateTimeOffset:O}");
                    return true;
                }

                if (sosPoint == null)
                {
                    break;
                }

                var aqtsValue = (dataRequest.ApplyRounding ?? false)
                    ? double.Parse(aqtsPoint.Value.Display)
                    : aqtsPoint.Value.Numeric;

                var sosValue = sosPoint.Value.Numeric;

                if (sosPoint.Timestamp.DateTimeOffset != aqtsPoint.Timestamp.DateTimeOffset)
                {
                    Log.Warn($"'{timeSeriesIdentifier}': Different timestamps: AQUARIUS={aqtsValue}@{aqtsPoint.Timestamp.DateTimeOffset:O} vs SOS={sosValue}@{sosPoint.Timestamp.DateTimeOffset:O}");
                    return true;
                }

                if (!DoubleHelper.AreSame(aqtsValue, sosValue))
                {
                    Log.Warn($"'{timeSeriesIdentifier}': Different values @ {aqtsPoint.Timestamp.DateTimeOffset:O}: AQUARIUS={aqtsValue} vs SOS={sosValue}");
                    return true;
                }

                sosPoints.Dequeue();
                aqtsPoints.Dequeue();
            }

            Log.Info($"'{timeSeriesDescription.Identifier}': All {sosCount} SOS points match between SOS and AQUARIUS.");
            dataRequest.QueryFrom = lastSensorTime.Value.AddTicks(1);

            return false;
        }

        private void TrimExcludedPoints(
            TimeSeriesDescription timeSeriesDescription,
            TimeSeriesDataServiceResponse timeSeries,
            DateTime? nextChangesSinceToken)
        {
            var (exportDuration, exportLabel) = GetExportDuration(timeSeriesDescription);

            if (exportDuration <= TimeSpan.Zero || !timeSeries.Points.Any())
                return;

            var firstTimeToInclude = timeSeries.Points.Last().Timestamp.DateTimeOffset - exportDuration;
            var firstTimeToExclude = nextChangesSinceToken.HasValue
                ? new DateTimeOffset(nextChangesSinceToken.Value)
                : (DateTimeOffset?)null;

            var nonFuturePoints = timeSeries.Points
                .Where(p => p.Timestamp.DateTimeOffset < firstTimeToExclude)
                .ToList();

            var futurePointCount = timeSeries.Points.Count - nonFuturePoints.Count;

            var remainingPoints = nonFuturePoints
                .Where(p => p.Timestamp.DateTimeOffset >= firstTimeToInclude)
                .ToList();

            if (remainingPoints.Count > Context.MaximumPointsPerSensor)
            {
                remainingPoints = remainingPoints
                    .Skip(remainingPoints.Count - Context.MaximumPointsPerSensor)
                    .ToList();
            }

            var earlyPointCount = timeSeries.Points.Count - remainingPoints.Count;

            var excludedPointCount = futurePointCount + earlyPointCount;

            if (excludedPointCount <= 0)
                return;

            Log.Info($"Trimming '{timeSeriesDescription.Identifier}' {"point".ToQuantity(earlyPointCount)} before {firstTimeToInclude:O} and {"point".ToQuantity(futurePointCount)} after {firstTimeToExclude:O} with {remainingPoints.Count} points remaining with ExportDuration={exportLabel}");

            timeSeries.Points = remainingPoints;
            timeSeries.NumPoints = timeSeries.Points.Count;
        }

        private (TimeSpan ExportDuration, string ExportLabel) GetExportDuration(TimeSeriesDescription timeSeriesDescription)
        {
            var durationAttribute = timeSeriesDescription
                .ExtendedAttributes
                .FirstOrDefault(a => a.Name.Equals(Context.Config.ExportDurationAttributeName,
                    StringComparison.InvariantCultureIgnoreCase));

            var attributeValue = durationAttribute?.Value as string;

            return (ParseHumanDuration(attributeValue) ?? TimeSpan.FromDays(Context.Config.DefaultExportDurationDays),
                !string.IsNullOrWhiteSpace(attributeValue) ? attributeValue : nameof(Context.Config.DefaultExportDurationDays));
        }

        private TimeSpan? ParseHumanDuration(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = HumanDurationRegex.Match(text);

            if (!match.Success)
                return null;

            var value = match.Groups["value"].Value;
            var unit = match.Groups["unit"].Value.TrimEnd('s', 'S');

            if (!double.TryParse(value, out var quantity) || quantity < 0)
                return null;

            if (!DurationFactory.TryGetValue(unit, out var factory))
                return null;

            return factory(quantity);
        }

        private static readonly Regex HumanDurationRegex = new Regex(@"^\s*(?<value>\S+)\s+(?<unit>\S+)\s*$");

        private static readonly Dictionary<string, Func<double, TimeSpan>> DurationFactory =
            new Dictionary<string, Func<double, TimeSpan>>(StringComparer.InvariantCultureIgnoreCase)
            {
                {"day", value => TimeSpan.FromDays(value)},
                {"week", value => TimeSpan.FromDays(value * 7)},
                {"month", value => TimeSpan.FromDays(value * 30)},
                {"year", value => TimeSpan.FromDays(value * 365)},
            };

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
    }
}
