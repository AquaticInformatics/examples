using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using Humanizer;
using Microsoft.VisualBasic.FileIO;
using NodaTime;
using ServiceStack.Logging;

namespace PointZilla.PointReaders
{
    public class CsvReader : CsvReaderBase, IPointReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public CsvReader(Context context)
            : base(context)
        {
            ValidateConfiguration(Context);
        }

        public (List<TimeSeriesPoint> Points, List<TimeSeriesNote> Notes) LoadPoints()
        {
            var points = Context
                .CsvFiles
                .SelectMany(LoadPoints)
                .ToList();

            var notes = LoadNotes();

            if (!Context.IgnoreNotes && notes.Any())
                Log.Info($"Loaded {"note".ToQuantity(notes.Count)}");

            return (points, notes);
        }

        private List<TimeSeriesPoint> LoadPoints(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"File '{path}' does not exist.");

            var points = LoadExcelPoints(path) ?? LoadCsvPoints(path);

            var anyGapPoints = points.Any(p => p.Type == PointType.Gap);

            if (Context.CsvRemoveDuplicatePoints && !anyGapPoints)
            {
                points = points
                    .OrderBy(p => p.Time)
                    .ToList();

                var duplicatePointCount = 0;

                for (var i = 1; i < points.Count; ++i)
                {
                    var prevPoint = points[i - 1];
                    var point = points[i];

                    if (point.Time != prevPoint.Time)
                        continue;

                    ++duplicatePointCount;

                    Log.Warn($"Discarding duplicate CSV point at {point.Time} with value {point.Value}");
                    points.RemoveAt(i);

                    --i;
                }

                if (duplicatePointCount > 0)
                {
                    Log.Warn($"Removed {duplicatePointCount} duplicate CSV points.");
                }
            }

            if (Context.CsvRealign && !anyGapPoints)
            {
                points = points
                    .OrderBy(p => p.Time)
                    .ToList();

                if (points.Any())
                {
                    // ReSharper disable once PossibleInvalidOperationException
                    var delta = points.First().Time.Value - Context.StartTime;

                    foreach (var point in points)
                    {
                        // ReSharper disable once PossibleInvalidOperationException
                        point.Time = point.Time.Value.Minus(delta);
                    }
                }
            }

            Log.Info($"Loaded {PointSummarizer.Summarize(points, "point")} from '{path}'.");

            return points;
        }

        private List<TimeSeriesPoint> LoadExcelPoints(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var excelReader = LoadExcelReader(stream))
            {
                return excelReader == null ? null : LoadPoints(excelReader);
            }
        }

        private IExcelDataReader LoadExcelReader(Stream stream)
        {
            try
            {
                return ExcelReaderFactory.CreateReader(stream);
            }
            catch (HeaderException)
            {
                return null;
            }
        }

        private List<TimeSeriesPoint> LoadPoints(IExcelDataReader excelReader)
        {
            var skipRows = Context.CsvSkipRows;

            var dataSet = excelReader.AsDataSet(new ExcelDataSetConfiguration
            {
                FilterSheet = (tableReader, sheetIndex) =>
                {
                    if (Context.ExcelSheetNumber.HasValue)
                        return sheetIndex == Context.ExcelSheetNumber.Value - 1;

                    if (!string.IsNullOrEmpty(Context.ExcelSheetName))
                        return tableReader.Name.Equals(Context.ExcelSheetName, StringComparison.InvariantCultureIgnoreCase);

                    // Load the first sheet by default
                    return sheetIndex == 0;
                },
                ConfigureDataTable = tableReader => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = Context.CsvHasHeaderRow,

                    ReadHeaderRow = rowReader =>
                    {
                        for (; skipRows > 0; --skipRows)
                        {
                            rowReader.Read();
                        }
                    }
                }
            });

            if (dataSet.Tables.Count < 1)
            {
                if (!string.IsNullOrEmpty(Context.ExcelSheetName))
                    throw new ExpectedException($"Can't find Excel worksheet '{Context.ExcelSheetName}'");

                throw new ExpectedException($"Can't find Excel worksheet number {Context.ExcelSheetNumber ?? 1}");
            }

            var table = dataSet.Tables[0];

            ValidateHeaderFields(table
                .Columns
                .Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToArray());

            return table
                .Rows
                .Cast<DataRow>()
                .Skip(skipRows)
                .Select(ParseExcelRow)
                .Where(p => p != null)
                .ToList();
        }

        private TimeSeriesPoint ParseExcelRow(DataRow row)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;

            if (!string.IsNullOrEmpty(Context.CsvComment) && ((row[0] as string)?.StartsWith(Context.CsvComment) ?? false))
                return null;

            try
            {
                if (Context.CsvDateOnlyField != null)
                {
                    var dateOnly = DateTime.MinValue;
                    var timeOnly = DefaultTimeOfDay;

                    ParseExcelColumn<DateTime>(row, Context.CsvDateOnlyField.ColumnIndex, dateTime => dateOnly = dateTime.Date);

                    if (Context.CsvTimeOnlyField != null)
                    {
                        ParseExcelColumn<DateTime>(row, Context.CsvTimeOnlyField.ColumnIndex, dateTime => timeOnly = dateTime.TimeOfDay);
                    }

                    time = InstantFromDateTime(dateOnly.Add(timeOnly));
                }
                else
                {
                    ParseExcelColumn<DateTime>(row, Context.CsvDateTimeField.ColumnIndex, dateTime => time = InstantFromDateTime(dateTime));
                }

                if (string.IsNullOrEmpty(Context.CsvNanValue))
                {
                    ParseExcelColumn<double>(row, Context.CsvValueField.ColumnIndex, number => value = number);
                }
                else
                {
                    // Detecting the NaN value is a bit more tricky.
                    // The column might have been converted as a pure string like "NA" or it could be a double like -9999.0
                    ParseValidExcelColumn(row, Context.CsvValueField.ColumnIndex, item =>
                    {
                        if (!(item is string itemText))
                        {
                            itemText = Convert.ToString(item);
                        }

                        if (Context.CsvNanValue == itemText)
                            return;

                        if (item is double number)
                            value = number;
                    });
                }

                ParseExcelColumn<double>(row, Context.CsvGradeField?.ColumnIndex, number => gradeCode = (int)number);
                ParseExcelStringColumn(row, Context.CsvQualifiersField?.ColumnIndex, text => qualifiers = QualifiersParser.Parse(text));

                return new TimeSeriesPoint
                {
                    Time = time,
                    Value = value,
                    GradeCode = gradeCode,
                    Qualifiers = qualifiers
                };
            }
            catch (Exception)
            {
                if (Context.CsvIgnoreInvalidRows)
                    return null;

                throw;
            }
        }

        private static void ParseExcelColumn<T>(DataRow row, int? fieldIndex, Action<T> parseAction) where T : struct
        {
            ParseValidExcelColumn(row, fieldIndex, item => parseAction((T)item));
        }

        private static void ParseExcelStringColumn(DataRow row, int? fieldIndex, Action<string> parseAction)
        {
            ParseValidExcelColumn(row, fieldIndex, item =>
            {
                if (item is string text && !string.IsNullOrWhiteSpace(text))
                    parseAction(text);
            });
        }

        private static void ParseValidExcelColumn(DataRow row, int? fieldIndex, Action<object> parseAction)
        {
            if (!fieldIndex.HasValue)
                return;

            if (fieldIndex > 0 && row.Table.Columns.Count > fieldIndex - 1)
            {
                var item = row[fieldIndex.Value - 1];

                if (item != null)
                {
                    parseAction(item);
                }
            }
        }

        private List<TimeSeriesPoint> LoadCsvPoints(string path)
        {
            var points = new List<TimeSeriesPoint>();

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
                    ValidateHeaderFields(fields);
                    parseHeaderRow = false;

                    if (Context.CsvHasHeaderRow)
                        continue;
                }

                var point = ParsePoint(fields);

                if (point == null)
                {
                    if (Context.CsvIgnoreInvalidRows) continue;

                    throw new ExpectedException($"Can't parse '{path}' ({lineNumber}): {string.Join(", ", fields)}");
                }

                points.Add(point);
            }

            return points;
        }

        private TimeSeriesPoint ParsePoint(string[] fields)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;
            PointType? pointType = null;

            if (Context.CsvDateOnlyField != null)
            {
                var dateOnly = DateTime.MinValue;
                var timeOnly = DefaultTimeOfDay;

                ParseField(fields, Context.CsvDateOnlyField.ColumnIndex, text =>
                {
                    if (TryParsePointType(text, out var pType))
                    {
                        pointType = pType;
                        return;
                    }

                    dateOnly = ParseDateOnly(text, Context.CsvDateOnlyFormat);
                });

                if (Context.CsvTimeOnlyField != null)
                {
                    ParseField(fields, Context.CsvTimeOnlyField.ColumnIndex, text => timeOnly = ParseTimeOnly(text, Context.CsvTimeOnlyFormat));
                }

                time = InstantFromDateTime(dateOnly.Add(timeOnly));
            }
            else
            {
                ParseField(fields, Context.CsvDateTimeField.ColumnIndex, text =>
                {
                    if (TryParsePointType(text, out var pType))
                    {
                        pointType = pType;
                        return;
                    }

                    time = ParseInstant(text);
                });
            }

            ParseField(fields, Context.CsvValueField.ColumnIndex, text =>
            {
                if (TryParsePointType(text, out var pType))
                {
                    pointType = pType;
                    return;
                }

                if (Context.CsvNanValue == text)
                    return;

                if (double.TryParse(text, out var numericValue))
                    value = numericValue;
            });

            ParseField(fields, Context.CsvGradeField?.ColumnIndex, text =>
            {
                if (int.TryParse(text, out var grade))
                    gradeCode = grade;
            });

            ParseField(fields, Context.CsvQualifiersField?.ColumnIndex, text => qualifiers = QualifiersParser.Parse(text));

            if ((pointType == null || pointType == PointType.Unknown) && time == null)
                return null;

            if (pointType != PointType.Gap)
                pointType = null;

            if (string.IsNullOrWhiteSpace(Context.CsvNotesFile))
            {
                ParseField(fields, Context.CsvNotesField?.ColumnIndex, text =>
                {
                    if (time.HasValue)
                        AddRowNote(time.Value, text);
                });
            }

            return new TimeSeriesPoint
            {
                Type = pointType,
                Time = time,
                Value = value,
                GradeCode = gradeCode,
                Qualifiers = qualifiers
            };
        }

        private List<TimeSeriesNote> LoadNotes()
        {
            if (RowNotes.Any())
                return RowNotes;

            return new CsvNotesReader(Context)
                .LoadNotes();
        }

        private static DateTime ParseDateOnly(string text, string format)
        {
            var dateTime = string.IsNullOrEmpty(format)
                ? DateTime.Parse(text)
                : DateTime.ParseExact(text, format, CultureInfo.InvariantCulture);

            return dateTime.Date;
        }

        private bool TryParsePointType(string text, out PointType pointType)
        {
            return PointTypes.TryGetValue(text, out pointType);
        }

        private static readonly Dictionary<string, PointType> PointTypes =
            new Dictionary<string, PointType>(StringComparer.InvariantCultureIgnoreCase)
            {
                {PointType.Gap.ToString(), PointType.Gap},
            };
    }
}
