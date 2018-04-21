using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;

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
            Log.Info($"Connecting {GetExecutingFileVersion()} to {Context.Server} ...");

            using (Client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} ({Client.ServerVersion})");

                PollForChanges();
            }
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

            var response = Client.Publish.Get(new TimeSeriesDescriptionServiceRequest { LocationIdentifier = location });

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == timeSeriesIdentifier);

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{timeSeriesIdentifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

        private TimeSeriesDescription GetTimeSeriesDescription(Guid uniqueId)
        {
            if (_knownTimeSeries.TryGetValue(uniqueId, out var timeSeriesDescription))
                return timeSeriesDescription;

            timeSeriesDescription = Client.Publish
                .Get(new TimeSeriesDescriptionListByUniqueIdServiceRequest
                {
                    TimeSeriesUniqueIds = new List<Guid> {uniqueId}
                })
                .TimeSeriesDescriptions
                .Single();

            _knownTimeSeries.Add(uniqueId, timeSeriesDescription);

            return timeSeriesDescription;
        }

        private readonly Dictionary<Guid,TimeSeriesDescription> _knownTimeSeries = new Dictionary<Guid, TimeSeriesDescription>();

        private void PollForChanges()
        {
            var pollInterval = Context.PollInterval.ToTimeSpan();

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

            var changesSinceToken = Context.ChangesSinceTime;

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                request.ChangesSinceToken = changesSinceToken.ToDateTimeUtc();

                var stopwatch = Stopwatch.StartNew();
                var response = Client.Publish.Get(request);
                MeasurePollResponseTime(stopwatch.ElapsedMilliseconds, response);

                var nextToken = Instant.FromDateTimeUtc(response.NextToken ?? DateTime.UtcNow);
                changesSinceToken = nextToken;

                var hasTokenExpired = response.TokenExpired ?? false;

                if (hasTokenExpired)
                {
                    Log.Warn($"The changes since token expired. Some data may have been missed. Resetting to 'right now' {changesSinceToken}");
                    continue;
                }

                var changedSeries = response.TimeSeriesUniqueIds;

                if (TimeSeries.Any())
                {
                    changedSeries = changedSeries.Where(ts => TimeSeries.ContainsKey(ts.UniqueId)).ToList();
                }

                if (changedSeries.Any())
                {
                    ShowChangedSeries(changedSeries);

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
                var firstLocation = TimeSeries.Values.First().LocationIdentifier;

                if (TimeSeries.Values.All(ts => ts.LocationIdentifier == firstLocation))
                    locationIdentifier = firstLocation;
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

            foreach (var series in changedSeries)
            {
                var description = GetTimeSeriesDescription(series.UniqueId);

                var firstPointChange = series.FirstPointChanged.HasValue
                    ? series.FirstPointChanged.Value.ToString("O")
                    : "None";

                var attributeChange = series.HasAttributeChange ?? false;

                Log.Info($"UniqueId={series.UniqueId:N} FirstPoint={firstPointChange} AttributeChange={attributeChange,-5} {description.Identifier}");
            }

            ChangeCount += changedSeries.Count;
        }
    }
}
