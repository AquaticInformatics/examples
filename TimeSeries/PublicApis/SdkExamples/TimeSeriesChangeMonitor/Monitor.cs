using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using NodaTime;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Text;

namespace TimeSeriesChangeMonitor
{
    public class Monitor
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private IAquariusClient Client { get; set; }
        private Dictionary<Guid,TimeSeriesDescription> TimeSeries { get; set; }
        private int ChangeCount { get; set; }

        public void Run()
        {
            Validate();

            Log.Info($"Connecting {GetExecutingFileVersion()} to {Context.Server} ...");

            using (Client = CreateConnectedClient())
            {
                Log.Info($"Connected to {Context.Server} ({Client.ServerVersion})");

                PollForChanges();
            }
        }

        private void Validate()
        {
            if (!string.IsNullOrEmpty(Context.DetectedChangesCsv) && Context.MaximumChangeCount > 0)
                throw new ExpectedException($"Only one of /{nameof(Context.DetectedChangesCsv)}= or /{nameof(Context.MaximumChangeCount)}= should be set.");

            if (!string.IsNullOrEmpty(Context.DetectedChangesCsv))
                Context.MaximumChangeCount = 1; // Always exit after one detected change
        }

        private IAquariusClient CreateConnectedClient()
        {
            return string.IsNullOrWhiteSpace(Context.SessionToken)
                ? AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password)
                : AquariusClient.ClientFromExistingSession(Context.Server, Context.SessionToken);
        }

        private static string GetExecutingFileVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // ReSharper disable once PossibleNullReferenceException
            return $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} v{fileVersionInfo.FileVersion}";
        }

        private Guid GetTimeSeriesUniqueId(string timeSeriesIdentifier)
        {
            if (Guid.TryParse(timeSeriesIdentifier, out var uniqueId))
                return uniqueId;

            var location = TimeSeriesIdentifierParser.ParseLocationIdentifier(timeSeriesIdentifier);

            var response = Client.Publish.Get(new TimeSeriesDescriptionServiceRequest {LocationIdentifier = location});

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == timeSeriesIdentifier);

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{timeSeriesIdentifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

        private TimeSeriesDescription GetTimeSeriesDescription(Guid uniqueId)
        {
            FetchAllUnknownSeriesDescriptions(new []{uniqueId});

            return _knownTimeSeries[uniqueId];
        }

        private readonly Dictionary<Guid,TimeSeriesDescription> _knownTimeSeries = new Dictionary<Guid, TimeSeriesDescription>();

        private void FetchAllUnknownSeriesDescriptions(IEnumerable<Guid> uniqueIds)
        {
            var unknownTimeSeriesUniqueIds = uniqueIds
                .Where(id => !_knownTimeSeries.ContainsKey(id))
                .ToList();

            // A regular `GET /Publish/v2/GetTimeSeriesDescriptionListByUniqueId` request is limited by the IIS max query string length of 2048 bytes.
            // That means about 61 unique IDs per request before IIS refuses the request.
            // Instead, we use a ServiceStack override method that allows use to send the request parameters as a JSON payload.
            // This allows us to request up to 400 time-series per request, the maximum allowed by the API.
            using (var batchClient = CreatePublishClientWithPostMethodOverride())
            {
                while (unknownTimeSeriesUniqueIds.Any())
                {
                    const int batchSize = 400;

                    var batchList = unknownTimeSeriesUniqueIds.Take(batchSize).ToList();
                    unknownTimeSeriesUniqueIds = unknownTimeSeriesUniqueIds.Skip(batchSize).ToList();

                    var request = new TimeSeriesDescriptionListByUniqueIdServiceRequest();

                    // We need to resolve the URL without any unique IDs on the GET command line
                    var requestUrl = RemoveQueryFromUrl(request.ToGetUrl());

                    request.TimeSeriesUniqueIds = batchList;

                    var batchResponse =
                        batchClient.Send<TimeSeriesDescriptionListByUniqueIdServiceResponse>(HttpMethods.Post, requestUrl, request);

                    foreach (var timeSeriesDescription in batchResponse.TimeSeriesDescriptions)
                    {
                        _knownTimeSeries.Add(timeSeriesDescription.UniqueId, timeSeriesDescription);
                    }
                }
            }
        }

        private JsonServiceClient CreatePublishClientWithPostMethodOverride()
        {
            return Client.CloneAuthenticatedClientWithOverrideMethod(Client.Publish, HttpMethods.Get) as JsonServiceClient;
        }

        private static string RemoveQueryFromUrl(string url)
        {
            var queryIndex = url.IndexOf("?", StringComparison.InvariantCulture);

            if (queryIndex < 0)
                return url;

            return url.Substring(0, queryIndex);
        }

        private static readonly TimeSpan ShortestAllowedInterval = TimeSpan.FromMinutes(5);

        private void PollForChanges()
        {
            var pollInterval = Context.PollInterval.ToTimeSpan();

            if (pollInterval < ShortestAllowedInterval)
            {
                if (!Context.AllowQuickPolling)
                    throw new ExpectedException($"Polling more quickly than every {ShortestAllowedInterval.Humanize()} is not enabled.");

                Log.Warn($"Polling more quickly than every {ShortestAllowedInterval.Humanize()} is not recommended for production systems.");
            }

            var request = CreateFilterRequest();
            var filterSummary = GetFilterSummary(request);

            Log.Info($"Monitoring {filterSummary} every {pollInterval.Humanize()}");
            Log.Info("Press Ctrl+C or Ctrl+Break to exit.");

            var cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var changesSinceToken = FetchChangesSinceToken();

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                request.ChangesSinceToken = changesSinceToken?.ToDateTimeUtc();

                var stopwatch = Stopwatch.StartNew();
                var response = Client.Publish.Get(request);
                MeasurePollResponseTime(stopwatch.ElapsedMilliseconds, response);

                var nextToken = Instant.FromDateTimeUtc(response.NextToken ?? DateTime.UtcNow);
                changesSinceToken = nextToken;

                var hasTokenExpired = response.TokenExpired ?? false;

                if (hasTokenExpired)
                {
                    Log.Warn($"The changes since token expired. Some data may have been missed. Resetting to 'right now' {changesSinceToken}");

                    SaveAllNewSeries(request);
                    continue;
                }

                var changedSeries = GetChangedTimeSeries(response.TimeSeriesUniqueIds);

                SaveNextChangesSince(changesSinceToken);

                if (changedSeries.Any())
                {
                    ShowChangedSeries(changedSeries);

                    SaveDetectedChanges(changedSeries, response);

                    if (Context.MaximumChangeCount > 0 && ChangeCount >= Context.MaximumChangeCount)
                        break;

                    Log.Info($"Sleeping for {pollInterval.Humanize()} ...");
                }

                cancellationTokenSource.Token.WaitHandle.WaitOne(pollInterval);
            }

            SummaryPollResponseStatistics();

            if (cancellationTokenSource.IsCancellationRequested)
                throw new ExpectedException($"Polling canceled by user after detecting {ChangeCount} changed time-series.");

            Log.Info($"Exiting after detecting {ChangeCount} changed time-series.");
        }

        private Instant? FetchChangesSinceToken()
        {
            if (Context.ChangesSinceTime.HasValue)
                return Context.ChangesSinceTime;

            if (!string.IsNullOrEmpty(Context.SavedChangesSinceJson))
            {
                if (!File.Exists(Context.SavedChangesSinceJson))
                    return null;

                var state = File.ReadAllText(Context.SavedChangesSinceJson).FromJson<State>();

                return state.ChangesSince;
            }

            return Instant.FromDateTimeUtc(DateTime.UtcNow);
        }

        private void SaveNextChangesSince(Instant? nextChangesSinceToken)
        {
            if (string.IsNullOrEmpty(Context.SavedChangesSinceJson))
                return;

            var state = new State {ChangesSince = nextChangesSinceToken};

            File.WriteAllText(Context.SavedChangesSinceJson, state.ToJson().IndentJson());
        }

        private class State
        {
            public Instant? ChangesSince { get; set; }
        }

        private List<TimeSeriesUniqueIds> GetChangedTimeSeries(List<TimeSeriesUniqueIds> allChangedTimeSeries)
        {
            return !TimeSeries.Any()
                ? allChangedTimeSeries
                : allChangedTimeSeries.Where(ts => TimeSeries.ContainsKey(ts.UniqueId)).ToList();
        }

        private void MeasurePollResponseTime(long elapsedMilliseconds, TimeSeriesUniqueIdListServiceResponse response)
        {
            foreach (var timingBucket in TimingBuckets)
            {
                if (timingBucket.UpperBoundMilliseconds <= elapsedMilliseconds) continue;

                timingBucket.TotalMilliseconds += elapsedMilliseconds;
                timingBucket.ResponseCount += 1;
                timingBucket.DetectedChangeCount += response.TimeSeriesUniqueIds.Count;
                break;
            }
        }

        private class TimingBucket
        {
            public long UpperBoundMilliseconds { get; set; }
            public long TotalMilliseconds { get; set; }
            public int ResponseCount { get; set; }
            public int DetectedChangeCount { get; set; }

            public string Summarize()
            {
                if (ResponseCount == 0)
                    return null;

                var name = UpperBoundMilliseconds == 0
                    ? "Total"
                    : UpperBoundMilliseconds == long.MaxValue
                        ? "Longer"
                        : $"<= {UpperBoundMilliseconds.Milliseconds().Humanize()}";

                return $"{name}: DetectedChanges={DetectedChangeCount} ResponseCount={ResponseCount} AverageTime={(TotalMilliseconds / ResponseCount).Milliseconds().Humanize()}";
            }
        }

        private List<TimingBucket> TimingBuckets { get; set; } =
            new[] {50L, 100L, 250L, 1000L, 2000L, long.MaxValue}
                .OrderBy(upperBound => upperBound)
                .Select(upperBound => new TimingBucket {UpperBoundMilliseconds = upperBound})
                .ToList();

        private void SummaryPollResponseStatistics()
        {
            var summaryBucket = new TimingBucket
            {
                ResponseCount = TimingBuckets.Sum(t => t.ResponseCount),
                TotalMilliseconds = TimingBuckets.Sum(t => t.TotalMilliseconds),
                DetectedChangeCount = TimingBuckets.Sum(t => t.DetectedChangeCount)
            };

            if (summaryBucket.ResponseCount == 0)
            {
                Log.Info("No responses received");
                return;
            }

            var summaries = new List<string>{summaryBucket.Summarize()};
            summaries.AddRange(TimingBuckets.Select(t => t.Summarize()));

            foreach (var summary in summaries.Where(s => !string.IsNullOrEmpty(s)))
            {
                Log.Info($"Response Statistics: {summary}");
            }
        }

        private TimeSeriesUniqueIdListServiceRequest CreateFilterRequest()
        {
            var locationIdentifier = Context.LocationIdentifier;

            if (!string.IsNullOrEmpty(locationIdentifier))
            {
                var locationDescription = Client.Publish
                    .Get(new LocationDescriptionListServiceRequest {LocationIdentifier = locationIdentifier})
                    .LocationDescriptions
                    .SingleOrDefault();

                if (locationDescription == null)
                    throw new ExpectedException($"Location '{locationIdentifier}' does not exist.");

                locationIdentifier = locationDescription.Identifier;
            }

            TimeSeries = Context.TimeSeries.Select(GetTimeSeriesUniqueId)
                .ToDictionary(
                    uniqueId => uniqueId,
                    GetTimeSeriesDescription);

            if (TimeSeries.Any())
            {
                var firstTimeSeries = TimeSeries.Values.First();

                if (TimeSeries.Values.All(ts => ts.LocationIdentifier == firstTimeSeries.LocationIdentifier))
                    locationIdentifier = firstTimeSeries.LocationIdentifier;

                if (string.IsNullOrEmpty(Context.Parameter) && TimeSeries.Values.All(ts => ts.Parameter == firstTimeSeries.Parameter))
                    Context.Parameter = firstTimeSeries.Parameter;

                if (!Context.Publish.HasValue && TimeSeries.Values.All(ts => ts.Publish == firstTimeSeries.Publish))
                    Context.Publish = firstTimeSeries.Publish;
            }

            if (!string.IsNullOrEmpty(locationIdentifier) && TimeSeries.Values.Any(ts => ts.LocationIdentifier != locationIdentifier))
            {
                locationIdentifier = null;
            }

            return new TimeSeriesUniqueIdListServiceRequest
            {
                LocationIdentifier = locationIdentifier,
                ChangeEventType = Context.ChangeEventType?.ToString(),
                Publish = Context.Publish,
                Parameter = Context.Parameter,
                ComputationIdentifier = Context.ComputationIdentifier,
                ComputationPeriodIdentifier = Context.ComputationPeriodIdentifier,
                ExtendedFilters = Context.ExtendedFilters.Any() ? Context.ExtendedFilters : null,
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

            sb.Append(" for ");

            sb.Append(Context.MaximumChangeCount > 0
                ? $"up to {"change".ToQuantity(Context.MaximumChangeCount)}"
                : "changes");

            sb.Append(TimeSeries.Any() ? $" in {TimeSeries.Count}" : " in any");

            sb.Append(" time-series");

            return sb.ToString();
        }

        private void ShowChangedSeries(List<TimeSeriesUniqueIds> changedSeries)
        {
            Log.Info($"Detected {changedSeries.Count} changed time-series");

            FetchAllUnknownSeriesDescriptions(changedSeries
                .Where(ts => !(ts.IsDeleted ?? false))
                .Select(ts => ts.UniqueId));

            foreach (var series in changedSeries)
            {
                if (series.IsDeleted ?? false)
                {
                    var identifier = _knownTimeSeries.TryGetValue(series.UniqueId, out var existingDescription)
                        ? existingDescription.Identifier
                        : null;

                    Log.Info($"UniqueId={series.UniqueId:N} Deleted {identifier}");
                    continue;
                }

                var description = GetTimeSeriesDescription(series.UniqueId);

                var firstPointChange = series.FirstPointChanged.HasValue
                    ? series.FirstPointChanged.Value.ToString("O")
                    : "None";

                var attributeChange = series.HasAttributeChange ?? false;

                Log.Info($"UniqueId={series.UniqueId:N} FirstPoint={firstPointChange} AttributeChange={attributeChange,-5} {description.Identifier}");
            }

            ChangeCount += changedSeries.Count;
        }

        private void SaveAllNewSeries(TimeSeriesUniqueIdListServiceRequest request)
        {
            if (string.IsNullOrEmpty(Context.DetectedChangesCsv))
                return;

            // Fetch all of the time-series that match the filter
            request.ChangesSinceToken = null;

            Log.Info($"Fetching all time-series for initial sync ...");

            var response = Client.Publish.Get(request);

            var newSeriesUniqueIds = response
                .TimeSeriesUniqueIds
                .Where(ts => !(ts.IsDeleted ?? false))
                .Select(ts => ts.UniqueId)
                .ToList();

            FetchAllUnknownSeriesDescriptions(newSeriesUniqueIds);

            var changedSeries = GetChangedTimeSeries(response.TimeSeriesUniqueIds);

            SaveDetectedChanges(changedSeries, response, true);
        }

        private void SaveDetectedChanges(List<TimeSeriesUniqueIds> changedSeries, TimeSeriesUniqueIdListServiceResponse response, bool isFullSync = false)
        {
            if (string.IsNullOrEmpty(Context.DetectedChangesCsv))
                return;

            if (!response.NextToken.HasValue)
                isFullSync = true;

            var detectedChanges = changedSeries
                .Select(ts => Convert(ts, isFullSync))
                .ToList();

            var csvOutput = CsvSerializer.SerializeToCsv(detectedChanges);

            Log.Info(csvOutput);

            File.WriteAllText(Context.DetectedChangesCsv, csvOutput);
        }

        private DetectedChange Convert(TimeSeriesUniqueIds detectedEvent, bool isFullSync)
        {
            var eventType = (detectedEvent.IsDeleted ?? false)
                ? DetectedEventType.Deleted
                : isFullSync
                    ? DetectedEventType.FullSync
                    : (detectedEvent.HasAttributeChange ?? false)
                        ? DetectedEventType.AttributeChanged
                        : DetectedEventType.DataChanged;

            var timeSeriesIdentifier =
                _knownTimeSeries.TryGetValue(detectedEvent.UniqueId, out var timeSeriesDescription)
                    ? timeSeriesDescription.Identifier
                    : null;

            return new DetectedChange
            {
                TimeSeriesUniqueId = detectedEvent.UniqueId,
                TimeSeriesIdentifier = timeSeriesIdentifier,
                EventType = eventType,
                FirstPointChangedUtc = detectedEvent.FirstPointChanged,
                LastMatchedTimeUtc = detectedEvent.LastMatchedTime,
            };
        }

        public class DetectedChange
        {
            public Guid TimeSeriesUniqueId { get; set; }
            public DetectedEventType EventType { get; set; }
            public string TimeSeriesIdentifier { get; set; }
            public DateTimeOffset? FirstPointChangedUtc { get; set; }
            public DateTimeOffset? LastMatchedTimeUtc { get; set; }

        }

        public enum DetectedEventType
        {
            FullSync,
            AttributeChanged,
            DataChanged,
            Deleted,
        }
    }
}
