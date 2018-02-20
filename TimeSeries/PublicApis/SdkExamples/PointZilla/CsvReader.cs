using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
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

            var points = new List<ReflectedTimeSeriesPoint>();

            var parser = new TextFieldParser(path)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] {","},
                TrimWhiteSpace = true
            };

            if (!string.IsNullOrWhiteSpace(Context.CsvComment))
            {
                parser.CommentTokens = new[] {Context.CsvComment};
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

            if (Context.CsvRealign)
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

        private ReflectedTimeSeriesPoint ParsePoint(string[] fields)
        {
            Instant? time = null;
            double? value = null;
            int? gradeCode = null;
            List<string> qualifiers = null;

            ParseField(fields, Context.CsvTimeField, text => time = ParseTime(text));
            ParseField(fields, Context.CsvValueField, text =>
            {
                if (double.TryParse(text, out var numericValue))
                    value = numericValue;
            });
            ParseField(fields, Context.CsvGradeField, text =>
            {
                if (int.TryParse(text, out var grade))
                    gradeCode = grade;
            });
            ParseField(fields, Context.CsvQualifiersField, text => qualifiers = text.Split(QualifierDelimeters, StringSplitOptions.RemoveEmptyEntries).ToList());

            if (time == null)
                return null;

            return new ReflectedTimeSeriesPoint
            {
                Time = time,
                Value = value,
                GradeCode = gradeCode,
                Qualifiers = qualifiers
            };
        }

        private static readonly char[] QualifierDelimeters = {','};

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
