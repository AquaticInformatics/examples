using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        private List<TimeSeriesPoint> Points { get; set; }

        public PointsAppender(Context context)
        {
            Context = context;
        }

        public void AppendPoints()
        {
            Log.Info(Context.ExecutingFileVersion);

            Points = GetPoints();

            if (Points.All(p => p.Type != PointType.Gap))
            {
                Points = Points
                    .OrderBy(p => p.Time)
                    .ToList();
            }

            ThrowIfInvalidGapInterval();

            AdjustGradesAndQualifiers(Points);

            if (!string.IsNullOrEmpty(Context.SaveCsvPath))
            {
                new CsvWriter(Context)
                    .WritePoints(Points);

                if (Context.StopAfterSavingCsv)
                    return;
            }

            Log.Info($"Connecting to {Context.Server} ...");

            using (var client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} ({client.ServerVersion})");

                ThrowIfGapsNotSupported(client);

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
                var stopwatch = Stopwatch.StartNew();

                var pointBatches = GetPointBatches(Points).ToList();
                var isBatched = pointBatches.Count > 1;
                var batchIndex = 1;

                foreach (var batch in pointBatches)
                {
                    if (isBatched)
                    {
                        var batchSummary =
                            $"Appending batch #{batchIndex}: {batch.Points.Count} points [{batch.Points.First().Time} to {batch.Points.Last().Time}]";

                        Log.Info( hasTimeRange
                            ? $"{batchSummary} within TimeRange={batch.TimeRange} ..."
                            : $"{batchSummary} ...");
                    }

                    var result = AppendPointBatch(client, timeSeries, batch.Points, batch.TimeRange, isReflected, hasTimeRange);
                    numberOfPointsAppended += result.NumberOfPointsAppended;
                    numberOfPointsDeleted += result.NumberOfPointsDeleted;
                    ++batchIndex;

                    if (result.AppendStatus != AppendStatusCode.Completed)
                        throw new ExpectedException($"Unexpected append status={result.AppendStatus}");
                }

                var batchText = isBatched ? $" using {pointBatches.Count} appends" : "";
                Log.Info($"Appended {numberOfPointsAppended} points (deleting {numberOfPointsDeleted} points) in {stopwatch.ElapsedMilliseconds / 1000.0:F1} seconds{batchText}.");
            }
        }

        private void ThrowIfInvalidGapInterval()
        {
            if (Points.Any() && (Points.First().Type == PointType.Gap || Points.Last().Type == PointType.Gap))
                throw new ExpectedException($"You can only insert manual {nameof(PointType.Gap)} points in between valid timestamps.");
        }

        private void ThrowIfGapsNotSupported(IAquariusClient client)
        {
            if (Points.Any(p => p.Type == PointType.Gap) && client.ServerVersion.IsLessThan(MinimumGapVersion))
                throw new ExpectedException($"You can't insert manual {nameof(PointType.Gap)} points before AQTS v{MinimumGapVersion}");
        }

        private static readonly AquariusServerVersion MinimumGapVersion = AquariusServerVersion.Create("19.1");

        private TimeSeriesAppendStatus AppendPointBatch(IAquariusClient client, TimeSeries timeSeries, List<TimeSeriesPoint> points, Interval timeRange, bool isReflected, bool hasTimeRange)
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
                if (hasTimeRange)
                {
                    appendResponse = client.Acquisition.Post(new PostTimeSeriesOverwriteAppend
                    {
                        UniqueId = timeSeries.UniqueId,
                        TimeRange = timeRange,
                        Points = points
                    });
                }
                else
                {
                    appendResponse = client.Acquisition.Post(new PostTimeSeriesAppend
                    {
                        UniqueId = timeSeries.UniqueId,
                        Points = points
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

        private void AdjustGradesAndQualifiers(List<TimeSeriesPoint> points)
        {
            if (!Context.IgnoreGrades && !Context.IgnoreQualifiers && !Context.GradeMappingEnabled && !Context.QualifierMappingEnabled)
                return;

            foreach (var point in points)
            {
                point.GradeCode = AdjustGradeCode(point.GradeCode);
                point.Qualifiers = AdjustQualifiers(point.Qualifiers);
            }
        }

        private int? AdjustGradeCode(int? gradeCode)
        {
            if (Context.IgnoreGrades)
                return null;

            if (!Context.GradeMappingEnabled)
                return gradeCode;

            return gradeCode.HasValue
                ? Context.MappedGrades.TryGetValue(gradeCode.Value, out var mappedValue)
                    ? mappedValue
                    : gradeCode
                : Context.MappedDefaultGrade;
        }

        private List<string> AdjustQualifiers(List<string> qualifiers)
        {
            if (Context.IgnoreQualifiers)
                return null;

            if (!Context.QualifierMappingEnabled)
                return qualifiers;

            if (qualifiers == null || !qualifiers.Any())
                return Context.MappedDefaultQualifiers;

            var mappedQualifiers = qualifiers
                .Select(s => Context.MappedQualifiers.TryGetValue(s, out var mappedValue) ? mappedValue : s)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return mappedQualifiers.Any()
                ? mappedQualifiers
                : Context.MappedDefaultQualifiers;
        }

        private IEnumerable<(List<TimeSeriesPoint> Points, Interval TimeRange)> GetPointBatches(
            List<TimeSeriesPoint> points)
        {
            var remainingTimeRange = GetTimeRange();

            var index = 0;
            while (points.Count - index > Context.BatchSize)
            {
                var batchPoints = points.Skip(index).Take(Context.BatchSize).ToList();
                var batchTimeRange = new Interval(remainingTimeRange.Start, batchPoints.Last().Time.GetValueOrDefault().PlusTicks(1));
                remainingTimeRange = new Interval(batchTimeRange.End, remainingTimeRange.End);

                yield return (batchPoints, batchTimeRange);

                index += Context.BatchSize;
            }

            yield return (points.Skip(index).ToList(), remainingTimeRange);
        }

        private List<TimeSeriesPoint> GetPoints()
        {
            if (Context.Command == CommandType.DeleteAllPoints)
                return new List<TimeSeriesPoint>();

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
