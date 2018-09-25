using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using NodaTime;
using ServiceStack.Logging;
using PostReflectedTimeSeries = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.PostReflectedTimeSeries;

namespace PointZilla
{
    public class PointsAppender
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }
        private List<ReflectedTimeSeriesPoint> Points { get; set; }

        public PointsAppender(Context context)
        {
            Context = context;
        }

        public void AppendPoints()
        {
            Points = GetPoints()
                .OrderBy(p => p.Time)
                .ToList();

            Log.Info($"Connecting to {Context.Server} ...");

            using (var client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} ({client.ServerVersion})");

                if (Context.CreateMode != CreateMode.Never)
                {
                    new TimeSeriesCreator
                    {
                        Context = Context,
                        Client = client
                    }.CreateMissingTimeSeries(Context.TimeSeries);
                }

                var timeSeries = client.GetTimeSeriesInfo(Context.TimeSeries);

                var isReflected = Context.Command == CommandType.Reflected || timeSeries.TimeSeriesType == TimeSeriesType.Reflected;
                var hasTimeRange = isReflected || Context.Command == CommandType.DeleteAllPoints || Context.Command == CommandType.OverwriteAppend;

                var pointExtents = Points.Any()
                    ? $"points [{Points.First().Time} to {Points.Last().Time}]"
                    : "points";

                Log.Info(Context.Command == CommandType.DeleteAllPoints
                    ? $"Deleting all existing points from {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ..."
                    : hasTimeRange
                        ? $"Appending {Points.Count} {pointExtents} within TimeRange={GetTimeRange()} to {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ..."
                        : $"Appending {Points.Count} {pointExtents} to {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ...");

                var numberOfPointsAppended = 0;
                var numberOfPointsDeleted = 0;
                var batchCount = 0;
                var stopwatch = Stopwatch.StartNew();

                foreach (var batch in GetPointBatches())
                {
                    var result = AppendPointBatch(client, timeSeries, batch.Item1, batch.Item2, isReflected, hasTimeRange);
                    numberOfPointsAppended += result.NumberOfPointsAppended;
                    numberOfPointsDeleted += result.NumberOfPointsDeleted;
                    batchCount += 1;

                    if (result.AppendStatus != AppendStatusCode.Completed)
                        throw new ExpectedException($"Unexpected append status={result.AppendStatus}");
                }

                var batchText = batchCount > 1 ? $" using {batchCount} appends" : "";
                Log.Info($"Appended {numberOfPointsAppended} points (deleting {numberOfPointsDeleted} points) in {stopwatch.ElapsedMilliseconds / 1000.0:F1} seconds{batchText}.");
            }
        }

        private TimeSeriesAppendStatus AppendPointBatch(IAquariusClient client, TimeSeries timeSeries, List<ReflectedTimeSeriesPoint> points, Interval timeRange, bool isReflected, bool hasTimeRange)
        {
            AppendResponse appendResponse;

            if (isReflected)
            {
                appendResponse = client.Acquisition.Post(new PostReflectedTimeSeries
                {
                    UniqueId = timeSeries.UniqueId,
                    TimeRange = timeRange,
                    Points = points
                });
            }
            else
            {
                var basicPoints = points
                    .Select(p => new TimeSeriesPoint
                    {
                        Time = p.Time,
                        Value = p.Value
                    })
                    .ToList();

                if (hasTimeRange)
                {
                    appendResponse = client.Acquisition.Post(new PostTimeSeriesOverwriteAppend
                    {
                        UniqueId = timeSeries.UniqueId,
                        TimeRange = timeRange,
                        Points = basicPoints
                    });
                }
                else
                {
                    appendResponse = client.Acquisition.Post(new PostTimeSeriesAppend
                    {
                        UniqueId = timeSeries.UniqueId,
                        Points = basicPoints
                    });
                }
            }

            return client.Acquisition.RequestAndPollUntilComplete(
                acquisition => appendResponse,
                (acquisition, response) => acquisition.Get(new GetTimeSeriesAppendStatus { AppendRequestIdentifier = response.AppendRequestIdentifier }),
                polledStatus => polledStatus.AppendStatus != AppendStatusCode.Pending,
                null,
                Context.AppendTimeout);
        }

        private IEnumerable<Tuple<List<ReflectedTimeSeriesPoint>, Interval>> GetPointBatches()
        {
            var points = GetPoints();
            var remainingTimeRange = GetTimeRange();

            var index = 0;
            while (points.Count - index > Context.BatchSize)
            {
                var batchPoints = points.Skip(index).Take(Context.BatchSize).ToList();
                var batchTimeRange = new Interval(remainingTimeRange.Start, batchPoints.Last().Time.GetValueOrDefault().PlusTicks(1));
                remainingTimeRange = new Interval(batchTimeRange.End, remainingTimeRange.End);

                yield return new Tuple<List<ReflectedTimeSeriesPoint>, Interval>(batchPoints, batchTimeRange);

                index += Context.BatchSize;
            }

            yield return new Tuple<List<ReflectedTimeSeriesPoint>, Interval>(points.Skip(index).ToList(), remainingTimeRange);
        }

        private List<ReflectedTimeSeriesPoint> GetPoints()
        {
            if (Context.Command == CommandType.DeleteAllPoints)
                return new List<ReflectedTimeSeriesPoint>();

            if (Context.ManualPoints.Any())
                return Context.ManualPoints;

            if (Context.SourceTimeSeries != null)
                return new ExternalPointsReader(Context)
                    .LoadPoints();

            if (Context.CsvFiles.Any())
                return new CsvReader(Context)
                    .LoadPoints();

            return new WaveformGenerator(Context)
                .GeneratePoints();
        }

        private Interval GetTimeRange()
        {
            if (Context.Command == CommandType.DeleteAllPoints)
                // Apply a 1-day margin, to workaround the AQ-23146 OverflowException crash
                return new Interval(
                    Instant.FromDateTimeOffset(DateTimeOffset.MinValue).Plus(Duration.FromStandardDays(1)),
                    Instant.FromDateTimeOffset(DateTimeOffset.MaxValue).Minus(Duration.FromStandardDays(1)));

            if (Context.TimeRange.HasValue)
                return Context.TimeRange.Value;

            if (!Points.Any())
                throw new ExpectedException($"Can't infer a time-range from an empty points list. Please set the /{nameof(Context.TimeRange)} option explicitly.");

            return new Interval(
                // ReSharper disable once PossibleInvalidOperationException
                Points.First().Time.Value,
                // ReSharper disable once PossibleInvalidOperationException
                Points.Last().Time.Value.PlusTicks(1));
        }
    }
}
