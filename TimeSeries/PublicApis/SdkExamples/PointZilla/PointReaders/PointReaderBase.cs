using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Humanizer;
using NodaTime;
using ILog = ServiceStack.Logging.ILog;
using LogManager = ServiceStack.Logging.LogManager;

namespace PointZilla.PointReaders
{
    public abstract class PointReaderBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Context Context { get; }
        protected TimeSpan DefaultTimeOfDay { get; }
        protected Duration DefaultBias { get; set; }
        protected List<TimeSeriesNote> RowNotes { get; } = new List<TimeSeriesNote>();
        private TimeSeriesNote CurrentNote { get; set; }

        protected PointReaderBase(Context context)
        {
            Context = context;

            DefaultTimeOfDay = ParseTimeOnly(Context.CsvDefaultTimeOfDay, Context.CsvTimeOnlyFormat);

            DefaultBias = Duration.FromTimeSpan((Context.UtcOffset ?? Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks)).ToTimeSpan());

            if (GetFields().Any(f => f.HasColumnName))
            {
                Context.CsvHasHeaderRow = true;
            }
        }

        protected static void ValidateConfiguration(Context context)
        {
            if (context.CsvDateOnlyField == null && context.CsvTimeOnlyField != null)
                throw new ExpectedException($"You can't mix the /{nameof(context.CsvDateTimeField)} option with the /{nameof(context.CsvTimeOnlyField)} option.");

            if (context.CsvDateTimeField == null && context.CsvDateOnlyField == null)
                throw new ExpectedException($"You need to specify either the /{nameof(context.CsvDateTimeField)} or /{nameof(context.CsvDateOnlyField)} options.");

            if (context.CsvValueField == null)
                throw new ExpectedException($"You must specify the /{nameof(context.CsvValueField)} option.");
        }

        private List<Field> GetFields()
        {
            return new[]
                {
                    Context.CsvDateTimeField,
                    Context.CsvDateOnlyField,
                    Context.CsvTimeOnlyField,
                    Context.CsvValueField,
                    Context.CsvGradeField,
                    Context.CsvQualifiersField,
                    string.IsNullOrWhiteSpace(Context.CsvNotesFile) ? Context.CsvNotesField : null,
                }
                .Where(f => f != null)
                .ToList();
        }

        protected void ValidateHeaderFields(string[] columnNames, List<Field> fields = null)
        {
            if (fields == null)
                fields = GetFields();

            var indexedFields = fields
                .Where(f => f.HasColumnIndex)
                .ToList();

            var errors = indexedFields
                .Where(f => f.ColumnIndex > columnNames.Length)
                .Select(f => $"{f.FieldName} index {f.ColumnIndex} exceeds the maximum field index of {columnNames.Length}.")
                .ToList();

            var namedFields = fields
                .Where(f => f.HasColumnName)
                .ToList();

            if (Context.CsvHasHeaderRow)
            {
                for (var i = 0; i < columnNames.Length; ++i)
                {
                    var field = columnNames[i];
                    var index = 1 + i;

                    foreach (var namedField in namedFields.Where(f => f.ColumnName.Equals(field, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (namedField.ColumnIndex == index)
                            continue;

                        if (namedField.HasColumnIndex)
                        {
                            errors.Add($"{namedField.FieldName}='{namedField.ColumnName}' is an ambiguous field name. Does it mean column {namedField.ColumnIndex} or {index}?");
                            continue;
                        }

                        namedField.ColumnIndex = index;
                    }
                }
            }

            foreach (var unknownField in namedFields.Where(f => !f.HasColumnIndex))
            {
                errors.Add($"{unknownField.FieldName}='{unknownField.ColumnName}' is an unknown column name.");
            }

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    Log.Error(error);
                }

                throw new ExpectedException($"{"column validation error".ToQuantity(errors.Count)} detected.");
            }
        }

        protected static TimeSpan ParseTimeOnly(string text, string format)
        {
            var dateTime = string.IsNullOrEmpty(format)
                ? DateTime.Parse(text)
                : DateTime.ParseExact(text, format, CultureInfo.InvariantCulture);

            return dateTime.TimeOfDay;
        }

        protected Instant InstantFromDateTime(DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Utc
                ? Instant.FromDateTimeUtc(dateTime)
                : Instant.FromDateTimeOffset(new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), DefaultBias.ToTimeSpan()));
        }

        protected void AddRowNote(Instant time, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                CurrentNote = null;
                return;
            }

            if (CurrentNote != null && CurrentNote.NoteText == text)
            {
                // Extend the last note
                CurrentNote.TimeRange = new Interval(CurrentNote.TimeRange?.Start ?? time, time);
                return;
            }

            CurrentNote = new TimeSeriesNote
            {
                TimeRange = new Interval(time, time),
                NoteText = text
            };

            RowNotes.Add(CurrentNote);
        }
    }
}
