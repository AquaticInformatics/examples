﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Humanizer;
using NodaTime;
using PointZilla.PointReaders;
using ServiceStack.Logging;
using PostReflectedTimeSeries = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.PostReflectedTimeSeries;

namespace PointZilla
{
    public class PointsAppender
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }
        private List<TimeSeriesPoint> Points { get; set; }
        private List<TimeSeriesNote> Notes { get; set; }

        public PointsAppender(Context context)
        {
            Context = context;
        }

        public void AppendPoints()
        {
            Log.Info(Context.ExecutingFileVersion);

            (Points, Notes) = GetPoints();

            AdjustNotes();

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
                    .WritePoints(Points, Notes);

                if (Context.StopAfterSavingCsv)
                    return;
            }

            Log.Info($"Connecting to {Context.Server} ...");

            using (var client = CreateConnectedClient())
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
                var hasTimeRange = isReflected || DeleteCommands.Contains(Context.Command) || Context.Command == CommandType.OverwriteAppend;

                if (hasTimeRange)
                {
                    var timeRange = GetTimeRange();

                    if (Notes.Any(note => note.TimeRange != null && (!timeRange.Contains(note.TimeRange.Value.Start) ||
                                                                     !timeRange.Contains(note.TimeRange.Value.End))))
                    {
                        throw new ExpectedException($"All notes to append must be completely within the {timeRange} interval.");
                    }
                }

                Log.Info(Context.Command == CommandType.DeleteAllPoints
                    ? $"Deleting all existing points from {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ..."
                    : hasTimeRange
                        ? $"Appending {PointSummarizer.Summarize(Points)} and {"note".ToQuantity(Notes.Count)} within TimeRange={GetTimeRange()} to {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ..."
                        : $"Appending {PointSummarizer.Summarize(Points)} and {"note".ToQuantity(Notes.Count)} to {timeSeries.Identifier} ({timeSeries.TimeSeriesType}) ...");

                var numberOfPointsAppended = 0;
                var numberOfPointsDeleted = 0;
                var numberOfNotesAppended = 0;
                var numberOfNotesDeleted = 0;
                var stopwatch = Stopwatch.StartNew();

                var pointBatches = GetPointBatches(Points).ToList();
                var isBatched = pointBatches.Count > 1;
                var batchIndex = 1;

                foreach (var batch in pointBatches)
                {
                    if (isBatched)
                    {
                        var batchSummary =
                            $"Appending batch #{batchIndex}: {PointSummarizer.Summarize(batch.Points)}";

                        Log.Info( hasTimeRange
                            ? $"{batchSummary} within TimeRange={batch.TimeRange} ..."
                            : $"{batchSummary} ...");
                    }

                    var result = AppendPointBatch(client, timeSeries, batch.Points, batch.TimeRange, isReflected, hasTimeRange);
                    numberOfPointsAppended += result.NumberOfPointsAppended;
                    numberOfPointsDeleted += result.NumberOfPointsDeleted;
                    ++batchIndex;

                    if (!ValidStatusCodesByWaitMode[Context.Wait].Contains(result.AppendStatus))
                        throw new ExpectedException($"Unexpected append status={result.AppendStatus}");
                }

                if (DeleteCommands.Contains(Context.Command))
                    numberOfNotesDeleted += DeleteNotesWithinTimeRange(client, timeSeries, GetTimeRange());
                else
                    numberOfNotesAppended += AppendNotes(client, timeSeries);
 
                var batchText = isBatched ? $" using {"append".ToQuantity(pointBatches.Count)}" : "";
                var waitText = Context.Wait ? string.Empty : " (without waiting for appends to complete)";

                Log.Info($"Appended {"point".ToQuantity(numberOfPointsAppended)} and {"note".ToQuantity(numberOfNotesAppended)} (deleting {"point".ToQuantity(numberOfPointsDeleted)} and {"note".ToQuantity(numberOfNotesDeleted)}) in {stopwatch.ElapsedMilliseconds / 1000.0:F1} seconds{batchText}{waitText}.");
            }
        }

        private static readonly Dictionary<bool, HashSet<AppendStatusCode>> ValidStatusCodesByWaitMode =
            new Dictionary<bool, HashSet<AppendStatusCode>>
            {
                { true, new HashSet<AppendStatusCode> { AppendStatusCode.Completed } },
                { false, new HashSet<AppendStatusCode> { AppendStatusCode.Completed, AppendStatusCode.Pending } },
            };

        private void AdjustNotes()
        {
            if (Context.IgnoreNotes)
            {
                Notes.Clear();
                return;
            }

            var noteCombiner = new NoteCombiner();

            foreach (var note in Notes
                .Select(AdjustNoteTextAndTimeRange)
                .OrderBy(note => note.TimeRange?.Start)
                .ThenByDescending(note => note.TimeRange?.End))
            {
                noteCombiner.AddOverlappingIntervalMetadata(note, (note1, note2) => note1.NoteText != note2.NoteText);
            }

            Notes = noteCombiner.Items;
        }

        private TimeSeriesNote AdjustNoteTextAndTimeRange(TimeSeriesNote note)
        {
            if (note.TimeRange.HasValue && note.TimeRange.Value.End == note.TimeRange.Value.Start)
            {
                note.TimeRange = new Interval(note.TimeRange.Value.Start, note.TimeRange.Value.End.PlusTicks(1));
            }

            const int maxNoteLength = 500;

            if (note.NoteText.Length > maxNoteLength)
            {
                note.NoteText = note.NoteText.Substring(0, maxNoteLength - 20) + " ... note truncated.";
            }

            return note;
        }

        private class NoteCombiner
        {
            public List<TimeSeriesNote> Items { get; } = new List<TimeSeriesNote>();

            private TimeSeriesNote CurrentItem { get; set; }

            public void AddOverlappingIntervalMetadata(
                TimeSeriesNote newItem,
                Func<TimeSeriesNote, TimeSeriesNote, bool> isDifferent)
            {
                var overlappingItem = Items
                    .LastOrDefault(item => item.TimeRange.Intersects(newItem.TimeRange) && !isDifferent(item, newItem));

                if (overlappingItem == null)
                {
                    CurrentItem = newItem;
                    Items.Add(CurrentItem);

                    return;
                }

                CurrentItem = overlappingItem;

                ExtendCurrentItem(newItem.TimeRange);
            }

            private void ExtendCurrentItem(Interval? timeRange)
            {
                if (!timeRange.HasValue || !CurrentItem.TimeRange.HasValue)
                    return;

                CurrentItem.TimeRange = new Interval(
                    Instant.Min(CurrentItem.TimeRange.Value.Start, timeRange.Value.Start),
                    Instant.Max(CurrentItem.TimeRange.Value.End, timeRange.Value.End));
            }
        }

        private IAquariusClient CreateConnectedClient()
        {
            return string.IsNullOrWhiteSpace(Context.SessionToken)
                ? AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password)
                : AquariusClient.ClientFromExistingSession(Context.Server, Context.SessionToken);
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

        private int DeleteNotesWithinTimeRange(IAquariusClient client, TimeSeries timeSeries, Interval timeRange)
        {
            if (Context.IgnoreNotes)
                return 0;

            if (client.ServerVersion.IsLessThan(MinimumNotesVersion))
                throw new ExpectedException($"You can't delete time-series notes before AQTS v{MinimumNotesVersion}");

            return client.Acquisition.Delete(new DeleteTimeSeriesNotes
            {
                UniqueId = timeSeries.UniqueId,
                TimeRange = timeRange
            }).NotesDeleted;
        }

        private int AppendNotes(IAquariusClient client, TimeSeries timeSeries)
        {
            if (Context.IgnoreNotes || !Notes.Any())
                return 0;

            if (client.ServerVersion.IsLessThan(MinimumNotesVersion))
                throw new ExpectedException($"You can't append time-series notes before AQTS v{MinimumNotesVersion}");

            return client.Acquisition.Post(new PostTimeSeriesMetadata
            {
                UniqueId = timeSeries.UniqueId,
                Notes = Notes
            }).NotesCreated;
        }

        private static readonly AquariusServerVersion MinimumNotesVersion = AquariusServerVersion.Create("19.2.185");

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
                else if (points.Any() || !Notes.Any())
                {
                    appendResponse = client.Acquisition.Post(new PostTimeSeriesAppend
                    {
                        UniqueId = timeSeries.UniqueId,
                        Points = points
                    });
                }
                else
                {
                    // We didn't have any basic points to append, but we do have notes to append.
                    // So just fake a completed status so we can move on to appending the notes.
                    return new TimeSeriesAppendStatus
                    {
                        AppendStatus = AppendStatusCode.Completed,
                    };
                }
            }

            return client.Acquisition.RequestAndPollUntilComplete(
                acquisition => appendResponse,
                (acquisition, response) => acquisition.Get(new GetTimeSeriesAppendStatus { AppendRequestIdentifier = response.AppendRequestIdentifier }),
                polledStatus => !Context.Wait || polledStatus.AppendStatus != AppendStatusCode.Pending,
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

        private (List<TimeSeriesPoint> Points, List<TimeSeriesNote> Notes) GetPoints()
        {
            if (DeleteCommands.Contains(Context.Command))
                return (new List<TimeSeriesPoint>(), new List<TimeSeriesNote>());

            if (Context.ManualPoints.Any())
                return (Context.ManualPoints, LoadNotes());

            if (Context.SourceTimeSeries != null)
                return new ExternalPointsReader(Context)
                    .LoadPoints();

            if (Context.CsvFiles.Any())
                return new CsvReader(Context)
                    .LoadPoints();

            if (Context.DbType.HasValue)
                return new DbPointsReader(Context)
                    .LoadPoints();

            if (!string.IsNullOrEmpty(Context.WaveFormTextX) || !string.IsNullOrEmpty(Context.WaveFormTextY))
                return (new TextGenerator(Context).GeneratePoints(), LoadNotes());

            return (new WaveformGenerator(Context)
                .GeneratePoints(), LoadNotes());
        }

        private static readonly HashSet<CommandType> DeleteCommands = new HashSet<CommandType>
        {
            CommandType.DeleteAllPoints,
            CommandType.DeleteTimeRange,
        };

        private List<TimeSeriesNote> LoadNotes()
        {
            return Context
                .ManualNotes
                .Concat(new CsvNotesReader(Context)
                    .LoadNotes())
                .ToList();
        }

        private Interval GetTimeRange()
        {
            if (Context.Command == CommandType.DeleteAllPoints)
                // Apply a 1-day margin, to workaround the AQ-23146 OverflowException crash
                return new Interval(
                    Instant.FromDateTimeOffset(DateTimeOffset.MinValue).Plus(Duration.FromDays(1)),
                    Instant.FromDateTimeOffset(DateTimeOffset.MaxValue).Minus(Duration.FromDays(1)));

            if (Context.TimeRange.HasValue)
                return Context.TimeRange.Value;

            if (!Points.Any())
            {
                if (Notes.Any())
                    return new Interval(Instant.MinValue, Instant.MaxValue);

                throw new ExpectedException($"Can't infer a time-range from an empty points list. Please set the /{nameof(Context.TimeRange)} option explicitly.");
            }

            return new Interval(
                // ReSharper disable once PossibleInvalidOperationException
                Points.First().Time.Value,
                // ReSharper disable once PossibleInvalidOperationException
                Points.Last().Time.Value.PlusTicks(1));
        }
    }
}
