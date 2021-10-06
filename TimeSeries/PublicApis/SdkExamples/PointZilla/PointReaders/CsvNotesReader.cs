using System.Collections.Generic;
using System.IO;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Microsoft.VisualBasic.FileIO;
using NodaTime;

namespace PointZilla.PointReaders
{
    public class CsvNotesReader : CsvReaderBase
    {
        public CsvNotesReader(Context context)
            : base(context)
        {
        }

        public List<TimeSeriesNote> LoadNotes()
        {
            if (string.IsNullOrEmpty(Context.CsvNotesFile))
                return new List<TimeSeriesNote>();

            if (!File.Exists(Context.CsvNotesFile))
                throw new ExpectedException($"File '{Context.CsvNotesFile}' does not exist.");

            return LoadNotes(Context.CsvNotesFile);
        }

        private List<TimeSeriesNote> LoadNotes(string path)
        {
            var notes = new List<TimeSeriesNote>();

            var csvDelimiter = string.IsNullOrEmpty(Context.CsvDelimiter)
                ? ","
                : Context.CsvDelimiter;

            var parser = new TextFieldParser(path)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] { csvDelimiter },
                TrimWhiteSpace = true,
                HasFieldsEnclosedInQuotes = true
            };

            if (!string.IsNullOrWhiteSpace(Context.CsvComment))
            {
                parser.CommentTokens = new[] { Context.CsvComment };
            }

            var skipCount = Context.CsvSkipRows;

            var parseHeaderRow = Context.CsvHasHeaderRow;

            while (!parser.EndOfData)
            {
                var lineNumber = parser.LineNumber;

                var fields = parser.ReadFields();
                if (fields == null) continue;

                if (skipCount > 0)
                {
                    --skipCount;
                    continue;
                }

                if (parseHeaderRow)
                {
                    ValidateHeaderFields(fields, new List<Field>
                    {
                        Context.NoteStartField,
                        Context.NoteEndField,
                        Context.NoteTextField,
                    });
                    parseHeaderRow = false;

                    if (Context.CsvHasHeaderRow)
                        continue;
                }

                var note = ParseNote(fields);

                if (note == null)
                {
                    if (Context.CsvIgnoreInvalidRows) continue;

                    throw new ExpectedException($"Can't parse '{path}' ({lineNumber}): {string.Join(", ", fields)}");
                }

                notes.Add(note);
            }

            return notes;
        }

        private TimeSeriesNote ParseNote(string[] fields)
        {
            Instant? start = null;
            Instant? end = null;
            var noteText = default(string);

            ParseField(fields, Context.NoteStartField.ColumnIndex, text => start = ParseInstant(text));
            ParseField(fields, Context.NoteEndField.ColumnIndex, text => end = ParseInstant(text));
            ParseField(fields, Context.NoteTextField.ColumnIndex, text => noteText = text);

            if (!start.HasValue || !end.HasValue || string.IsNullOrWhiteSpace(noteText))
                return null;

            if (end < start)
                return null;

            return new TimeSeriesNote
            {
                TimeRange = new Interval(start.Value, end.Value),
                NoteText = noteText
            };
        }
    }
}
