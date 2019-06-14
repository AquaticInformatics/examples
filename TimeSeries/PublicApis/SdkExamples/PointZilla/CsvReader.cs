using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using ExcelDataReader;
using ExcelDataReader.Exceptions;
using Microsoft.VisualBasic.FileIO;
using NodaTime;
using NodaTime.Text;
using ServiceStack.Logging;

namespace PointZilla
{
    public class CsvReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }
        private InstantPattern TimePattern { get; }

        public CsvReader(Context context)
        {
            Context = context;

            TimePattern = string.IsNullOrWhiteSpace(Context.CsvTimeFormat)
                ? InstantPattern.ExtendedIsoPattern
                : InstantPattern.CreateWithInvariantCulture(Context.CsvTimeFormat);
        }

        private Instant? ParseTime(string text)
        {
            var result = TimePattern.Parse(text);

            if (result.Success)
                return result.Value;

            return null;
        }

        public List<ReflectedTimeSeriesPoint> LoadPoints()
        {
            return Context.CsvFiles.SelectMany(LoadPoints)
                .ToList();
        }

        private List<ReflectedTimeSeriesPoint> LoadPoints(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"CSV file '{path}' does not exist.");

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

            Log.Info($"Loaded {points.Count} points from '{path}'.");

            return points;
        }

        private List<ReflectedTimeSeriesPoint> LoadExcelPoints(string path)
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

        private List<ReflectedTimeSeriesPoint> LoadPoints(IExcelDataReader excelReader)
        {
            var skipRows = Context.CsvSkipRows - 1;

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
                    UseHeaderRow = true,

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

            return table
                .Rows
                .Cast<DataRow>()
                .Select(ParseExcelRow)
                .Where(p => p != null)
                .ToList();
        }

        private ReflectedTimeSeriesPoint ParseExcelRow(DataRow row)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;

            if (!string.IsNullOrEmpty(Context.CsvComment) && ((row[0] as string)?.StartsWith(Context.CsvComment) ?? false))
                return null;

            ParseColumn<DateTime>(row, Context.CsvTimeField,
                dateTime => time = Instant.FromDateTimeOffset(new DateTimeOffset(dateTime, (Context.UtcOffset ?? Offset.Zero).ToTimeSpan())));

            ParseColumn<double>(row, Context.CsvValueField, number => value = number);
            ParseColumn<double>(row, Context.CsvGradeField, number => gradeCode = (int)number);
            ParseStringColumn(row, Context.CsvQualifiersField, text => qualifiers = ParseQualifiers(text));

            return new ReflectedTimeSeriesPoint
            {
                Time = time,
                Value = value,
                GradeCode = gradeCode,
                Qualifiers = qualifiers
            };
        }

        private static void ParseColumn<T>(DataRow row, int fieldIndex, Action<T> parseAction) where T : struct
        {
            if (fieldIndex > 0 && row.Table.Columns.Count > fieldIndex - 1)
            {
                var item = row[fieldIndex - 1];

                if (item != null)
                {
                    parseAction((T) item);
                }
            }
        }

        private static void ParseStringColumn(DataRow row, int fieldIndex, Action<string> parseAction)
        {
            if (fieldIndex > 0 && row.Table.Columns.Count > fieldIndex - 1)
            {
                if (row[fieldIndex - 1] is string item && !string.IsNullOrWhiteSpace(item))
                {
                    parseAction(item);
                }
            }
        }

        private List<ReflectedTimeSeriesPoint> LoadCsvPoints(string path)
        {
            var points = new List<ReflectedTimeSeriesPoint>();

            var parser = new TextFieldParser(path)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] { "," },
                TrimWhiteSpace = true
            };

            if (!string.IsNullOrWhiteSpace(Context.CsvComment))
            {
                parser.CommentTokens = new[] { Context.CsvComment };
            }

            var skipCount = Context.CsvSkipRows;

            while (!parser.EndOfData)
            {
                if (skipCount > 0)
                {
                    --skipCount;
                    continue;
                }

                var lineNumber = parser.LineNumber;

                var fields = parser.ReadFields();
                if (fields == null) continue;

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

        private ReflectedTimeSeriesPoint ParsePoint(string[] fields)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;
            PointType? pointType = null;

            ParseField(fields, Context.CsvTimeField, text =>
            {
                if (TryParsePointType(text, out var pType))
                {
                    pointType = pType;
                    return;
                }

                time = ParseTime(text);
            });
            ParseField(fields, Context.CsvValueField, text =>
            {
                if (TryParsePointType(text, out var pType))
                {
                    pointType = pType;
                    return;
                }

                if (double.TryParse(text, out var numericValue))
                    value = numericValue;
            });
            ParseField(fields, Context.CsvGradeField, text =>
            {
                if (int.TryParse(text, out var grade))
                    gradeCode = grade;
            });
            ParseField(fields, Context.CsvQualifiersField, text => qualifiers = ParseQualifiers(text));

            if ((pointType == null || pointType == PointType.Unknown) && time == null)
                return null;

            if (pointType != PointType.Gap)
                pointType = null;

            return new ReflectedTimeSeriesPoint
            {
                Type = pointType,
                Time = time,
                Value = value,
                GradeCode = gradeCode,
                Qualifiers = qualifiers
            };
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

        private static List<string> ParseQualifiers(string text)
        {
            return text
                .Split(QualifierDelimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private static readonly char[] QualifierDelimiters = {','};

        private static void ParseField(string[] fields, int fieldIndex, Action<string> parseAction)
        {
            if (fieldIndex > 0 && fields.Length > fieldIndex - 1)
            {
                var text = fields[fieldIndex - 1];

                if (!string.IsNullOrWhiteSpace(text))
                    parseAction(text);
            }
        }
    }
}
