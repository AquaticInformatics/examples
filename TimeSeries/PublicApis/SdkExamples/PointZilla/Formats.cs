using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualBasic.FileIO;

namespace PointZilla
{
    public static class Formats
    {
        public static string Description =>
            $"Shortcut for known CSV formats. One of {string.Join(", ", Formatters.Select(f => f.Id))}. [default: {Formatters.First().Id}]";

        public static void SetFormat(Context context, string value)
        {
            if (!FormatterLookup.TryGetValue(value, out var formatter))
                throw new ExpectedException($"'{value}' is an unknown CSV format.");

            formatter(context);
        }

        private static readonly IReadOnlyList<(string Id, Action<Context> Formatter)> Formatters = new (string Id, Action<Context> Formatter)[]
        {
            ("NG", SetNgCsvFormat),
            ("3X", Set3XCsvFormat),
            ("PointZilla", SetPointZillaCsvFormat),
            ("NWIS", SetNwisCsvFormat),
        };

        private static readonly Dictionary<string, Action<Context>> FormatterLookup =
            Formatters
                .ToDictionary(
                    f => f.Id,
                    f => f.Formatter,
                    StringComparer.InvariantCultureIgnoreCase);

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
            context.CsvSkipRowsAfterHeader = 0;
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
            context.CsvSkipRowsAfterHeader = 0;
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
            context.CsvSkipRowsAfterHeader = 0;
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

        public static void SetNwisCsvFormat(Context context)
        {
            // https://nwis.waterservices.usgs.gov/nwis/iv/?format=rdb&sites=01536000&period=P1D
            // # Data provided for site 01536000
            // #    TS_ID       Parameter Description
            // #    121787      00060     Discharge, cubic feet per second
            // #    121786      00065     Gage height, feet
            // #
            // # Data-value qualification codes included in this output:
            // #     P  Provisional data subject to revision.
            // #
            // agency_cd	site_no	datetime	tz_cd	121787_00060	121787_00060_cd	121786_00065	121786_00065_cd
            // 5s	15s	20d	6s	14n	10s	14n	10s
            // USGS	01536000	2022-03-23 12:15	EDT	629	P	3.42	P
            // USGS	01536000	2022-03-23 12:30	EDT	629	P	3.42	P
            // USGS	01536000	2022-03-23 12:45	EDT	629	P	3.42	P
            // USGS	01536000	2022-03-23 13:00	EDT	629	P	3.42	P
            context.CsvDelimiter = "\t";
            context.CsvSkipRows = 0;
            context.CsvSkipRowsAfterHeader = 1; // Skips that "5s	15s	20d	6s	14n	10s	14n	10s" line after the header
            context.CsvComment = "#";
            context.CsvDateTimeField = Field.Parse("datetime", nameof(context.CsvDateTimeField));
            context.CsvDateTimeFormat = "yyyy-MM-dd HH:mm";
            context.CsvDateOnlyField = null;
            context.CsvTimeOnlyField = null;
            context.CsvTimezoneField = Field.Parse("tz_cd", nameof(context.CsvTimezoneField));
            context.CsvValueField = Field.Parse("/_00060/", nameof(context.CsvValueField)); // Match discharge by default
            context.CsvGradeField = null;
            context.CsvQualifiersField = null;
            context.CsvIgnoreInvalidRows = true;
            context.CsvRealign = false;

            AddNwisTimezoneAliases(context.TimezoneAliases);
        }

        private static void AddNwisTimezoneAliases(Dictionary<string, string> aliases)
        {
            var text = FetchNwisZoneDefinitions();

            // #
            // # National Water Information System
            // # 2022/03/24
            // #
            // #
            // # Date Retrieved: USGS Water Data for the Nation Help System
            // #
            // tz_cd	tz_nm	tz_ds	tz_utc_offset_tm	tz_dst_cd	tz_dst_nm	tz_dst_utc_offset_tm
            // 5s	31s	34s	6s	6s	31s	6s
            // ACST	Central Australia Standard Time	Central Australia	+09:30	ACSST	Central Australia Summer Time	+10:30
            // AEST	Australia Eastern Standard Time	Eastern Australia	+10:00	AESST	Australia Eastern Summer Time	+11:00
            // AFT	Afghanistan Time	Afghanistan	+04:30	 	 	 
            // AKST	Alaska Standard Time	Alaska	-09:00	AKDT	Alaska Daylight Time	-08:00
            // AST	Atlantic Standard Time (Canada)	Atlantic (Canada)	-04:00	ADT	Atlantic Daylight Time	-03:00

            using (var reader = new StringReader(text))
            {
                var parser = new TextFieldParser(reader)
                {
                    CommentTokens = new[] { "#" },
                    TextFieldType = FieldType.Delimited,
                    Delimiters = new[] { "\t" },
                    TrimWhiteSpace = true,
                    HasFieldsEnclosedInQuotes = true,
                };

                var skipRowsAfterHeader = 1;
                var columns = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();
                    if (fields == null) continue;

                    if (!columns.Any())
                    {
                        for (var i = 0; i < fields.Length; ++i)
                        {
                            columns[fields[i]] = i;
                        }
                        continue;
                    }

                    if (skipRowsAfterHeader > 0)
                    {
                        --skipRowsAfterHeader;
                        continue;
                    }

                    string ParseField(string name)
                    {
                        if (!columns.TryGetValue(name, out var index) || index >= fields.Length)
                            return null;

                        return fields[index];
                    }

                    string ParseUtcOffset(string name)
                    {
                        var value = ParseField(name);

                        if (string.IsNullOrEmpty(value))
                            return value;

                        if (value[0] == '+' || value[0] == '-')
                            return $"UTC{value}";

                        return $"UTC+{value}";
                    }

                    var standardCode = ParseField("tz_cd");
                    var standardOffset = ParseUtcOffset("tz_utc_offset_tm");
                    var daylightCode = ParseField("tz_dst_cd");
                    var daylightOffset = ParseUtcOffset("tz_dst_utc_offset_tm");

                    if (!string.IsNullOrEmpty(standardCode) && !string.IsNullOrEmpty(standardOffset))
                        aliases[standardCode] = standardOffset;

                    if (!string.IsNullOrEmpty(daylightCode) && !string.IsNullOrEmpty(daylightOffset))
                        aliases[daylightCode] = daylightOffset;
                }
            }
        }

        private static string FetchNwisZoneDefinitions()
        {
            try
            {
                // Try to fetch the latest
                return new WebClient().DownloadString("https://help.waterdata.usgs.gov/code/tz_query?fmt=rdb");
            }
            catch (Exception)
            {
                // If we are blocked from the internet, fall back to a recent copy
                return @"#
# National Water Information System
# 2022/03/24
#
#
# Date Retrieved: USGS Water Data for the Nation Help System
#
tz_cd	tz_nm	tz_ds	tz_utc_offset_tm	tz_dst_cd	tz_dst_nm	tz_dst_utc_offset_tm
5s	31s	34s	6s	6s	31s	6s
ACST	Central Australia Standard Time	Central Australia	+09:30	ACSST	Central Australia Summer Time	+10:30
AEST	Australia Eastern Standard Time	Eastern Australia	+10:00	AESST	Australia Eastern Summer Time	+11:00
AFT	Afghanistan Time	Afghanistan	+04:30	 	 	 
AKST	Alaska Standard Time	Alaska	-09:00	AKDT	Alaska Daylight Time	-08:00
AST	Atlantic Standard Time (Canada)	Atlantic (Canada)	-04:00	ADT	Atlantic Daylight Time	-03:00
AWST	Australia Western Standard Time	Western Australia	+08:00	AWSST	Australia Western Summer Time	+09:00
BT	Baghdad Time	Baghdad	+03:00	 	 	 
CAST	Central Australia Standard Time	Central Australia	+09:30	CADT	Central Australia Daylight Time	+10:30
CCT	China Coastal Time	China Coastal	+08:00	 	 	 
CET	Central European Time	Central Europe	+01:00	CETDST	Central European Daylight Time	+02:00
CST	Central Standard Time	Central North America	-06:00	CDT	Central Daylight Time	-05:00
DNT	Dansk Normal Time	Dansk	+01:00	 	 	 
DST	Dansk Summer Time	Dansk	+01:00	 	 	 
EAST	East Australian Standard Time	East Australia	+10:00	EASST	East Australian Summer Time	+11:00
EET	Eastern Europe Standard Time	Eastern Europe, Russia Zone 1	+02:00	EETDST	Eastern Europe Daylight Time	+03:00
EST	Eastern Standard Time	Eastern North America	-05:00	EDT	Eastern Daylight Time	-04:00
FST	French Summer Time	French	+01:00	FWT	French Winter Time	+02:00
GMT	Greenwich Mean Time	Great Britain	 00:00	BST	British Summer Time	+01:00
GST	Guam Standard Time	Guam Standard Time, Russia Zone 9	+10:00	 	 	 
HST	Hawaii Standard Time	Hawaii	-10:00	HDT	Hawaii Daylight Time	-09:00
IDLE	International Date Line, East	International Date Line, East	+12:00	 	 	 
IDLW	International Date Line, West	International Date Line, West	-12:00	 	 	 
IST	Israel Standard Time	Israel	+02:00	 	 	 
IT	Iran Time	Iran	+03:30	 	 	 
JST	Japan Standard Time	Japan Standard Time, Russia Zone 8	+09:00	 	 	 
JT	Java Time	Java	+07:30	 	 	 
KST	Korea Standard Time	Korea	+09:00	 	 	 
LIGT	Melbourne, Australia	Melbourne	+10:00	 	 	 
MET	Middle Europe Time	Middle Europe	+01:00	METDST	Middle Europe Daylight Time	+02:00
MEWT	Middle Europe Winter Time	Middle Europe	+01:00	MEST	Middle Europe Summer Time	+02:00
MEZ	Middle Europe Zone	Middle Europe	+01:00	 	 	 
MST	Mountain Standard Time	Mountain North America	-07:00	MDT	Mountain Daylight Time	-06:00
MT	Moluccas Time	Moluccas	+08:30	 	 	 
NFT	Newfoundland Standard Time	Newfoundland	-03:30	NDT	Newfoundland Daylight Time	-02:30
NOR	Norway Standard Time	Norway	+01:00	 	 	 
NST	Newfoundland Standard Time	Newfoundland	-03:30	NDT	Newfoundland Daylight Time	-02:30
NZST	New Zealand Standard Time	New Zealand	+12:00	NZDT	New Zealand Daylight Time	+13:00
NZT	New Zealand Time	New Zealand	+12:00	NZDT	New Zealand Daylight Time	+13:00
PST	Pacific Standard Time	Pacific North America	-08:00	PDT	Pacific Daylight Time	-07:00
SAT	South Australian Standard Time	South Australia	+09:30	SADT	South Australian Daylight Time	+10:30
SET	Seychelles Time	Seychelles	+01:00	 	 	 
SWT	Swedish Winter Time	Swedish	+01:00	SST	Swedish Summer Time	+02:00
UTC	Universal Coordinated Time	Universal Coordinated Time	 00:00	 	 	 
WAST	West Australian Standard Time	West Australia	+07:00	WADT	West Australian Daylight Time	+08:00
WAT	West Africa Time	West Africa	-01:00	 	 	 
WET	Western Europe	Western Europe	 00:00	WETDST	Western Europe Daylight Time	+01:00
WST	West Australian Standard Time	West Australian	+08:00	WDT	West Australian Daylight Time	+09:00
ZP-11	UTC -11 hours	UTC -11 hours	-11:00	 	 	 
ZP-2	UTC -2 hours	Zone UTC -2 Hours	-02:00	 	 	 
ZP-3	UTC -3 hours	Zone UTC -3 Hours	-03:00	 	 	 
ZP11	UTC +11 hours	Zone UTC +11 Hours	+11:00	 	 	 
ZP4	UTC +4 hours	Zone UTC +4 Hours	+04:00	 	 	 
ZP5	UTC +5 hours	Zone UTC +5 Hours	+05:00	 	 	 
ZP6	UTC +6 hours	Zone UTC +6 Hours	+06:00	 	 	 
";
            }
        }
    }
}
