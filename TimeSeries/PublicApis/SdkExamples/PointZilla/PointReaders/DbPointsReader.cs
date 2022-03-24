using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Humanizer;
using NodaTime;
using PointZilla.DbClient;
using ServiceStack.Logging;

namespace PointZilla.PointReaders
{
    public class DbPointsReader : PointReaderBase, IPointReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public DbPointsReader(Context context)
            : base(context)
        {
        }

        public (List<TimeSeriesPoint> Points, List<TimeSeriesNote> Notes) LoadPoints()
        {
            if (!Context.DbType.HasValue)
                throw new ExpectedException($"/{nameof(Context.DbType)} must be set");

            ValidateContext();

            var query = ResolveQuery(Context.DbQuery, nameof(Context.DbQuery));

            Log.Info($"Querying {Context.DbType} database for points ...");

            using (var dbClient = DbClientFactory.CreateOpened(Context.DbType.Value, Context.DbConnectionString))
            {
                var table = dbClient.ExecuteTable(query);

                ValidateTable(table);

                var points = table
                    .Rows.Cast<DataRow>()
                    .Select(ConvertRowToPoint)
                    .Where(point => point != null)
                    .ToList();

                var notes = LoadNotes(dbClient);

                Log.Info($"Loaded {PointSummarizer.Summarize(points)} and {"note".ToQuantity(notes.Count)} from the database source.");

                return (points, notes);
            }
        }

        private void ValidateContext()
        {
            if (string.IsNullOrWhiteSpace(Context.DbConnectionString))
                throw new ExpectedException($"You must specify the /{nameof(Context.DbConnectionString)}= option when /{nameof(Context.DbType)}={Context.DbType}");

            if (string.IsNullOrWhiteSpace(Context.DbQuery))
                throw new ExpectedException($"You must specify the /{nameof(Context.DbQuery)}= option when /{nameof(Context.DbType)}={Context.DbType}");

            ValidateConfiguration(Context);
        }

        private static string ResolveQuery(string query, string name)
        {
            if (string.IsNullOrEmpty(query))
                return query;

            if (!query.StartsWith("@"))
                return query;

            var queryPath = query.Substring(1);

            if (!File.Exists(queryPath))
                throw new ExpectedException($"{name} file '{queryPath}' does not exist.");

            return File.ReadAllText(queryPath);
        }

        private void ValidateTable(DataTable dataTable, List<Field> fields = null)
        {
            Context.CsvHasHeaderRow = true;

            ValidateHeaderFields(dataTable
                .Columns
                .Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToArray(), fields);
        }

        private TimeSeriesPoint ConvertRowToPoint(DataRow row)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;
            DateTimeZone zone = null;

            ParseNullableColumn<string>(row, Context.CsvTimezoneField?.ColumnIndex, text =>
            {
                if (Context.TimezoneAliases.TryGetValue(text, out var alias))
                    text = alias;

                TimezoneHelper.TryParseDateTimeZone(text, out zone);
            });

            if (Context.CsvDateOnlyField != null)
            {
                var dateOnly = DateTime.MinValue;
                var timeOnly = DefaultTimeOfDay;

                ParseColumn<DateTime>(row, Context.CsvDateOnlyField.ColumnIndex, dateTime => dateOnly = dateTime.Date);

                if (Context.CsvTimeOnlyField != null)
                {
                    ParseColumn<DateTime>(row, Context.CsvDateOnlyField.ColumnIndex, dateTime => timeOnly = dateTime.TimeOfDay);
                }

                time = InstantFromDateTime(dateOnly.Add(timeOnly), () => zone);
            }
            else
            {
                ParseColumn<DateTime>(row, Context.CsvDateTimeField.ColumnIndex, dateTime => time = InstantFromDateTime(dateTime));
            }

            ParseColumn<double>(row, Context.CsvValueField.ColumnIndex, number => value = number);

            ParseColumn<int>(row, Context.CsvGradeField?.ColumnIndex, grade => gradeCode = grade);

            ParseNullableColumn<string>(row, Context.CsvQualifiersField?.ColumnIndex, text => qualifiers = QualifiersParser.Parse(text));

            if (time == null)
                return null;

            ParseNullableColumn<string>(row, Context.CsvNotesField?.ColumnIndex, text =>
            {
                if (time.HasValue)
                    AddRowNote(time.Value, text);
            });

            return new TimeSeriesPoint
            {
                Time = time,
                Value = value,
                GradeCode = gradeCode,
                Qualifiers = qualifiers
            };
        }

        private List<TimeSeriesNote> LoadNotes(IDbClient dbClient)
        {
            if (RowNotes.Any())
                return RowNotes;

            if (string.IsNullOrWhiteSpace(Context.DbNotesQuery))
                return new List<TimeSeriesNote>();

            var query = ResolveQuery(Context.DbNotesQuery, nameof(Context.DbNotesQuery));

            var table = dbClient.ExecuteTable(query);

            ValidateTable(table, new List<Field>
            {
                Context.NoteStartField,
                Context.NoteEndField,
                Context.NoteTextField,
            });

            return table
                .Rows.Cast<DataRow>()
                .Select(ConvertRowToNote)
                .Where(note => note != null)
                .ToList();
        }

        private TimeSeriesNote ConvertRowToNote(DataRow row)
        {
            Instant? start = null;
            Instant? end = null;
            var noteText = default(string);

            ParseColumn<DateTime>(row, Context.NoteStartField.ColumnIndex, dateTime => start = InstantFromDateTime(dateTime));
            ParseColumn<DateTime>(row, Context.NoteEndField.ColumnIndex, dateTime => end = InstantFromDateTime(dateTime));
            ParseNullableColumn<string>(row, Context.NoteTextField.ColumnIndex, text => noteText = text);

            if (!start.HasValue || !end.HasValue || string.IsNullOrWhiteSpace(noteText))
                return null;

            return new TimeSeriesNote
            {
                TimeRange = new Interval(start.Value, end.Value),
                NoteText = noteText
            };
        }

        private void ParseColumn<T>(DataRow row, int? columnIndex, Action<T> parseAction) where T : struct
        {
            if (!columnIndex.HasValue || columnIndex <= 0 || columnIndex > row.Table.Columns.Count)
                return;

            var index = columnIndex.Value - 1;

            if (row.IsNull(index))
                return;

            var value = row[index];

            if (!(value is T typedValue))
            {
                typedValue = (T)Convert.ChangeType(value, typeof(T));
            }

            parseAction(typedValue);
        }

        private void ParseNullableColumn<T>(DataRow row, int? columnIndex, Action<T> parseAction) where T : class
        {
            if (!columnIndex.HasValue || columnIndex <= 0 || columnIndex > row.Table.Columns.Count)
                return;

            var index = columnIndex.Value - 1;

            if (row.IsNull(index))
                return;

            var value = row[index];

            if (!(value is T typedValue))
            {
                typedValue = (T)Convert.ChangeType(value, typeof(T));
            }

            if (typedValue != null)
                parseAction(typedValue);
        }
    }
}
