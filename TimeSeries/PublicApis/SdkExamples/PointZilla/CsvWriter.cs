﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Humanizer;
using NodaTime;
using NodaTime.Text;
using PointZilla.PointReaders;
using ServiceStack.Logging;
using PublishNote = Aquarius.TimeSeries.Client.ServiceModels.Publish.Note;

namespace PointZilla
{
    public class CsvWriter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public CsvWriter(Context context)
        {
            Context = context;
        }

        public void WritePoints(List<TimeSeriesPoint> points, List<TimeSeriesNote> notes)
        {
            var timeSeriesIdentifier = CreateTimeSeriesIdentifier();

            var csvPath = Directory.Exists(Context.SaveCsvPath)
                ? Path.Combine(Context.SaveCsvPath, SanitizeFilename($"{timeSeriesIdentifier.Identifier}.{CreatePeriod(Context.SourceQueryFrom, Context.SourceQueryTo)}.csv"))
                : Context.SaveCsvPath;

            Log.Info($"Saving {PointSummarizer.Summarize(points, "extracted point")} to '{csvPath}' ...");

            var dir = Path.GetDirectoryName(csvPath);

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var publishNotes = notes
                .Select(Convert)
                .ToList();

            var notesLookup = new MetadataLookup<PublishNote>(publishNotes);

            using (var writer = new StreamWriter(csvPath))
            {
                var offsetPattern = OffsetPattern.CreateWithInvariantCulture("m");
                var utcOffsetText = $"UTC{offsetPattern.Format(Context.UtcOffset ?? Offset.Zero)}";
                var period = CreatePeriod(Context.SourceQueryFrom ?? Instant.MinValue, Context.SourceQueryTo ?? Instant.MaxValue);

                writer.WriteLine($"# {Path.GetFileName(csvPath)} generated by {Context.ExecutingFileVersion}");
                writer.WriteLine($"#");
                writer.WriteLine($"# Time series identifier: {timeSeriesIdentifier.Identifier}");
                writer.WriteLine($"# Location: {timeSeriesIdentifier.LocationIdentifier}");
                writer.WriteLine($"# UTC offset: ({utcOffsetText})");
                writer.WriteLine($"# Value units: {Context.Unit}");
                writer.WriteLine($"# Value parameter: {timeSeriesIdentifier.Parameter}");
                writer.WriteLine($"# Interpolation type: {Context.InterpolationType}");
                writer.WriteLine($"# Time series type: {Context.TimeSeriesType}");
                writer.WriteLine($"#");
                writer.WriteLine($"# Export options: Corrected signal from {period.StartText} to {period.EndText}");
                writer.WriteLine($"#");
                writer.WriteLine($"# CSV data starts at line 15.");
                writer.WriteLine($"#");

                var optionalNotesHeader = Context.SaveNotesMode == SaveNotesMode.WithPoints
                    ? ", Notes"
                    : string.Empty;

                writer.WriteLine($"ISO 8601 UTC, Value, Grade, Qualifiers{optionalNotesHeader}");

                foreach (var point in points)
                {
                    var time = point.Time ?? Instant.MinValue;

                    var line = $"{InstantPattern.ExtendedIsoPattern.Format(time)}, {point.Value:G12}, {point.GradeCode}, {FormatQualifiers(point.Qualifiers)}";

                    if (Context.SaveNotesMode == SaveNotesMode.WithPoints)
                    {
                        var pointNotes = string.Join("\r\n", notesLookup.GetMany(time.ToDateTimeOffset()).Select(note => note.NoteText));

                        line += $", {CsvEscapedColumn(pointNotes)}";
                    }

                    writer.WriteLine(line);
                }

                if (Context.SaveNotesMode == SaveNotesMode.SeparateCsv)
                {
                    var notesCsvPath = Path.ChangeExtension(csvPath, ".Notes.csv");

                    Log.Info($"Saving {"extracted note".ToQuantity(notes.Count)} to '{notesCsvPath}' ...");

                    // ReSharper disable once AssignNullToNotNullAttribute
                    using (var notesWriter = new StreamWriter(notesCsvPath))
                    {
                        notesWriter.WriteLine($"# {Path.GetFileName(notesCsvPath)} generated by {Context.ExecutingFileVersion}");
                        notesWriter.WriteLine($"#");
                        notesWriter.WriteLine($"# Time series identifier: {timeSeriesIdentifier.Identifier}");
                        notesWriter.WriteLine($"# Location: {timeSeriesIdentifier.LocationIdentifier}");
                        notesWriter.WriteLine($"# UTC offset: ({utcOffsetText})");
                        notesWriter.WriteLine($"#");
                        notesWriter.WriteLine($"# Export options: Corrected signal notes from {period.StartText} to {period.EndText}");
                        notesWriter.WriteLine($"#");
                        notesWriter.WriteLine($"# CSV data starts at line 11.");
                        notesWriter.WriteLine($"#");
                        notesWriter.WriteLine($"StartTime, EndTime, NoteText");

                        foreach (var note in notes)
                        {
                            if (!note.TimeRange.HasValue)
                                continue;

                            notesWriter.WriteLine($"{InstantPattern.ExtendedIsoPattern.Format(note.TimeRange.Value.Start)}, {InstantPattern.ExtendedIsoPattern.Format(note.TimeRange.Value.End)}, {CsvEscapedColumn(note.NoteText)}");
                        }
                    }
                }
            }
        }

        private static PublishNote Convert(TimeSeriesNote note)
        {
            return new PublishNote
            {
                StartTime = note.TimeRange?.Start.ToDateTimeOffset() ?? DateTimeOffset.MinValue,
                EndTime = note.TimeRange?.End.ToDateTimeOffset() ?? DateTimeOffset.MaxValue,
                NoteText = note.NoteText
            };
        }

        public static void SetPointZillaCsvFormat(Context context)
        {
            // Match PointZilla Export format below

            // # CSV data starts at line 15.
            // # 
            // ISO 8601 UTC, Value, Grade, Qualifiers, Notes
            // 2015-12-04T00:01:00Z, 3.523200823975, 500, ,
            // 2015-12-04T00:02:00Z, 3.525279357147, 500, ,

            context.CsvSkipRows = 0;
            context.CsvComment = "#";
            context.CsvDateTimeField = Field.Parse("ISO 8601 UTC", nameof(context.CsvDateTimeField));
            context.CsvDateTimeFormat = null;
            context.CsvDateOnlyField = null;
            context.CsvTimeOnlyField = null;
            context.CsvValueField = Field.Parse("Value", nameof(context.CsvValueField));
            context.CsvGradeField = Field.Parse("Grade", nameof(context.CsvGradeField));
            context.CsvQualifiersField = Field.Parse("Qualifiers", nameof(context.CsvQualifiersField));
            context.CsvNotesField = Field.Parse("Notes", nameof(context.CsvNotesField));
            context.CsvIgnoreInvalidRows = true;
            context.CsvRealign = false;
        }

        private TimeSeriesIdentifier CreateTimeSeriesIdentifier()
        {
            if (Context.SourceTimeSeries != null)
                return Context.SourceTimeSeries;

            if (!string.IsNullOrEmpty(Context.TimeSeries))
                return TimeSeriesIdentifierParser.ParseExtendedIdentifier(Context.TimeSeries);

            string parameter;
            var label = "Points";
            var locationIdentifier = "PointZilla";

            if (DeleteCommands.Contains(Context.Command))
                parameter ="Deleted";

            else if (Context.ManualPoints.Any())
                parameter = "ManuallyEntered";
            else if (Context.CsvFiles.Any())
                parameter = "OtherCsvFile";
            else
                parameter = Context.WaveformType.ToString();

            return new TimeSeriesIdentifier
            {
                Parameter = parameter,
                Label = label,
                LocationIdentifier = locationIdentifier,
                Identifier = $"{parameter}.{label}@{locationIdentifier}"
            };
        }

        private static readonly HashSet<CommandType> DeleteCommands = new HashSet<CommandType>
        {
            CommandType.DeleteAllPoints,
            CommandType.DeleteTimeRange,
        };

        private static string CreatePeriod(Instant? startTime, Instant? endTime)
        {
            var start = startTime ?? Instant.MinValue;
            var end = endTime ?? Instant.MaxValue;

            if (start == Instant.MinValue && end == Instant.MaxValue)
                return "EntireRecord";

            var period = CreatePeriod(start, end);

            return $"{period.StartText}.{period.EndText}";
        }

        private static (string StartText, string EndText) CreatePeriod(Instant start, Instant end)
        {
            return (
                start == Instant.MinValue ? "StartOfRecord" : InstantPattern.ExtendedIsoPattern.Format(start),
                end == Instant.MaxValue ? "EndOfRecord" : InstantPattern.ExtendedIsoPattern.Format(end)
            );
        }

        private static string FormatQualifiers(List<string> qualifiers)
        {
            if (qualifiers == null || !qualifiers.Any())
                return string.Empty;

            return CsvEscapedColumn(string.Join(",", qualifiers));
        }

        private static string CsvEscapedColumn(string text)
        {
            return !CharactersRequiringEscaping.Any(text.Contains)
                ? text
                : $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private static readonly char[] CharactersRequiringEscaping = new[]
        {
            ',',
            '"',
            '\n',
            '\r'
        };

        private static string SanitizeFilename(string s)
        {
            return Path.GetInvalidFileNameChars().Aggregate(s, (current, ch) => current.Replace(ch, '_'));
        }
    }
}
