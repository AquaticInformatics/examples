using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using log4net;
using Microsoft.VisualBasic.FileIO;

namespace NEM12PreProcessor
{
    // Spec at https://www.aemo.com.au/-/media/files/electricity/nem/retail_and_metering/metering-procedures/2018/mdff-specification-nem12--nem13-v106.pdf?la=en
    // Decent simplification at https://www.momentumenergy.com.au/docs/default-source/default-document-library/guide-to-interpret-an-interval-meter-data-report.pdf?sfvrsn=8
    public class Parser
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private long LineNumber { get; set; }

        private MeterInfo Meter { get; set; }

        private class MeterInfo
        {
            public string NationalMeteringIdentifier { get; set; }
            public string RegisterId { get; set; }
            public string UnitOfMeasure { get; set; }
            public int IntervalMinutes { get; set; }
            public List<Point> Points { get; set; }
        }

        public class Point
        {
            public DateTime Time { get; set; }
            public double Value { get; set; }
            public string QualityMethod { get; set; }
            public string ReasonCode { get; set; }
            public string ReasonDescription { get; set; }
        }

        public void ProcessStream(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                var parser = new TextFieldParser(reader)
                {
                    TextFieldType = FieldType.Delimited,
                    Delimiters = new[] {","},
                    TrimWhiteSpace = true,
                    HasFieldsEnclosedInQuotes = true,
                };

                WriteHeader();

                var recordParsers = new Dictionary<int, (int MinimumFieldCount, Action<string[]> Parser)>
                {
                    {100, (2,Record100)},
                    {200, (9,Record200)},
                    {300, (2,Record300)},
                    {400, (6,Record400)},
                    {900, (1,Record900)},
                };

                while (!parser.EndOfData)
                {
                    LineNumber = parser.LineNumber;

                    var fields = parser.ReadFields();
                    if (fields == null) continue;

                    var recordField = fields[0];

                    if (!int.TryParse(recordField, out var recordId))
                    {
                        Error($"'{recordField}' is not a valid integer.");
                        continue;
                    }

                    if (!recordParsers.TryGetValue(recordId, out var recordParser))
                    {
                        Error($"{recordId} is not a supported record value.");
                        continue;
                    }

                    if (fields.Length < recordParser.MinimumFieldCount)
                    {
                        Error($"Too few fields. {recordParser.MinimumFieldCount} fields required but only {fields.Length} provided.");
                        continue;
                    }

                    recordParser.Parser(fields);
                }

                Flush();
            }
        }

        private void Error(string message)
        {
            Log.Error($"Line {LineNumber}: {message}");
        }

        private bool TryParseInt(string text, out int value)
        {
            if (int.TryParse(text, out value))
                return true;

            Error($"'{text}' is not a valid integer");
            return false;
        }

        private bool TryParseDate(string text, out DateTime value)
        {
            if (DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
                return true;

            Error($"'{text}' is not a valid date.");
            return false;
        }

        private void Record100(string[] fields)
        {
            if (fields[2] != "NEM12")
                Error($"'{fields[2]}' is not a supported file format.");
        }

        private void Record200(string[] fields)
        {
            Flush();

            var nmi = fields[1];
            var registerId = fields[3];
            var uom = fields[7];

            if (!TryParseInt(fields[8], out var intervalMinutes))
                return;

            if (intervalMinutes < 1 || intervalMinutes > 1440)
            {
                Error($"An IntervalLength of {intervalMinutes} is invalid");
                return;
            }

            Meter = new MeterInfo
            {
                NationalMeteringIdentifier = nmi,
                RegisterId = registerId,
                UnitOfMeasure = uom,
                IntervalMinutes = intervalMinutes,
            };
        }

        private void Record300(string[] fields)
        {
            Flush();

            if (Meter == null)
            {
                Error("Invalid 300 record. No previous meter exists.");
                return;
            }

            if (!TryParseDate(fields[1], out var date))
                return;

            var intervalCount = 1440 / Meter.IntervalMinutes;

            var expectedFieldCount = 5 + intervalCount;

            if (fields.Length < expectedFieldCount)
            {
                Error($"Too few fields. Only {fields.Length} fields but {expectedFieldCount} were expected.");
                return;
            }

            Meter.Points = new List<Point>(intervalCount);

            var intervalBaseIndex = 2;

            var index = intervalBaseIndex + intervalCount;

            var qualityMethod = fields[index];
            var reasonCode = fields[index + 1];
            var reasonDescription = fields[index + 2];

            for (var i = 0; i < intervalCount; ++i)
            {
                var qualityFlag = qualityMethod;

                if (!double.TryParse(fields[intervalBaseIndex + i], out var value))
                {
                    value = 0;
                    qualityFlag = "N";
                }

                Meter.Points.Add(new Point
                {
                    Time = date + TimeSpan.FromMinutes(Meter.IntervalMinutes * i),
                    Value = value,
                    QualityMethod = qualityFlag,
                    ReasonCode = reasonCode,
                    ReasonDescription = reasonDescription
                });
            }
        }

        private void Record400(string[] fields)
        {
            if (!TryParseInt(fields[1], out var startInterval))
                return;

            if (!TryParseInt(fields[2], out var endInterval))
                return;

            if (Meter?.Points == null)
            {
                Error("Out of sequence 400 record. No previous points available.");
                return;
            }

            if (startInterval < 1
                || startInterval > Meter.Points.Count
                || endInterval < 1
                || endInterval > Meter.Points.Count
                || endInterval < startInterval)
            {
                Error($"Invalid interval Start={startInterval} End={endInterval}");
                return;
            }

            var qualityMethod = fields[3];
            var reasonCode = fields[4];
            var reasonDescription = fields[5];

            for (var i = startInterval; i <= endInterval; ++i)
            {
                var point = Meter.Points[i - 1];

                point.QualityMethod = qualityMethod;
                point.ReasonCode = reasonCode;
                point.ReasonDescription = reasonDescription;
            }
        }

        private void Record900(string[] fields)
        {
            Flush();
        }

        private void Flush()
        {
            if (Meter?.Points == null)
                return;

            foreach (var point in Meter.Points)
            {
                WritePoint(point);
            }

            Meter.Points = null;
        }

        private void WriteHeader()
        {
            Console.WriteLine($"NationalMeteringIdentifier, RegisterId, UnitOfMeasure, Time, Value, QualityMethod, ReasonCode, ReasonDescription");
        }

        private void WritePoint(Point point)
        {
            Console.WriteLine($"{Meter.NationalMeteringIdentifier}, {Meter.RegisterId}, {Meter.UnitOfMeasure}, {point.Time:yyyy-MM-dd HH:mm}, {point.Value}, {point.QualityMethod}, {point.ReasonCode}, {point.ReasonDescription}");
        }
    }
}
