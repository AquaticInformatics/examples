using System;
using System.Collections.Generic;

namespace PointZilla
{
    public static class Formats
    {
        public static string Description =>
            "Shortcut for known CSV formats. One of 'NG', '3X', or 'PointZilla'. [default: NG]";

        public static void SetFormat(Context context, string value)
        {
            if (!Formatters.TryGetValue(value, out var formatter))
                throw new ExpectedException($"'{value}' is an unknown CSV format.");

            formatter(context);
        }

        private static readonly Dictionary<string, Action<Context>> Formatters =
            new Dictionary<string, Action<Context>>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "NG", SetNgCsvFormat },
                { "3X", Set3XCsvFormat },
                { "PointZilla", SetPointZillaCsvFormat },
            };

        public static void SetNgCsvFormat(Context context)
        {
            // Match AQTS 201x Export-from-Springboard CSV format

            // # Take Volume.CS1004@IM974363.EntireRecord.csv generated at 2018-09-14 05:03:15 (UTC-07:00) by AQUARIUS 18.3.79.0
            // # 
            // # Time series identifier: Take Volume.CS1004@IM974363
            // # Location: 20017_CS1004
            // # UTC offset: (UTC+12:00)
            // # Value units: m^3
            // # Value parameter: Take Volume
            // # Interpolation type: Instantaneous Totals
            // # Time series type: Basic
            // # 
            // # Export options: Corrected signal from Beginning of Record to End of Record
            // # 
            // # CSV data starts at line 15.
            // # 
            // ISO 8601 UTC, Timestamp (UTC+12:00), Value, Approval Level, Grade, Qualifiers
            // 2013-07-01T11:59:59Z,2013-07-01 23:59:59,966.15,Raw - yet to be review,200,
            // 2013-07-02T11:59:59Z,2013-07-02 23:59:59,966.15,Raw - yet to be review,200,
            // 2013-07-03T11:59:59Z,2013-07-03 23:59:59,966.15,Raw - yet to be review,200,

            context.CsvSkipRows = 0;
            context.CsvComment = "#";
            context.CsvDateTimeField = Field.Parse("ISO 8601 UTC", nameof(context.CsvDateTimeField));
            context.CsvDateTimeFormat = null;
            context.CsvDateOnlyField = null;
            context.CsvTimeOnlyField = null;
            context.CsvTimezoneField = null;
            context.CsvValueField = Field.Parse("Value", nameof(context.CsvValueField));
            context.CsvGradeField = Field.Parse("Grade", nameof(context.CsvGradeField));
            context.CsvQualifiersField = Field.Parse("Qualifiers", nameof(context.CsvQualifiersField));
            context.CsvIgnoreInvalidRows = true;
            context.CsvRealign = false;
        }

        public static void Set3XCsvFormat(Context context)
        {
            // Match AQTS 3.x Export format

            // ,Take Volume.CS1004@IM974363,Take Volume.CS1004@IM974363,Take Volume.CS1004@IM974363,Take Volume.CS1004@IM974363
            // mm/dd/yyyy HH:MM:SS,m^3,,,
            // Date-Time,Value,Grade,Approval,Interpolation Code
            // 07/01/2013 23:59:59,966.15,200,1,6
            // 07/02/2013 23:59:59,966.15,200,1,6

            context.CsvComment = null;
            context.CsvSkipRows = 2;
            context.CsvDateTimeField = Field.Parse("Date-Time", nameof(context.CsvDateTimeField));
            context.CsvDateTimeFormat = "MM/dd/yyyy HH:mm:ss";
            context.CsvDateOnlyField = null;
            context.CsvTimeOnlyField = null;
            context.CsvTimezoneField = null;
            context.CsvValueField = Field.Parse("Value", nameof(context.CsvValueField));
            context.CsvGradeField = Field.Parse("Grade", nameof(context.CsvGradeField));
            context.CsvQualifiersField = null;
            context.CsvIgnoreInvalidRows = true;
            context.CsvRealign = false;
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
            context.CsvTimezoneField = null;
            context.CsvValueField = Field.Parse("Value", nameof(context.CsvValueField));
            context.CsvGradeField = Field.Parse("Grade", nameof(context.CsvGradeField));
            context.CsvQualifiersField = Field.Parse("Qualifiers", nameof(context.CsvQualifiersField));
            context.CsvNotesField = Field.Parse("Notes", nameof(context.CsvNotesField));
            context.CsvIgnoreInvalidRows = true;
            context.CsvRealign = false;
        }
    }
}
