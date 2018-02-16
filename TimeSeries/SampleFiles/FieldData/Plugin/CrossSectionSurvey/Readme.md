# Sample Cross-Section Survey field data file

AQUARIUS Time-Series can import field data from Cross-Section Survey CSV files.

## AQUARIUS location identifiers

The `Location:` field should contain the AQUARIUS location identifier in order to support automatic upload from Springboard.

If the file does not contain an AQUARIUS location identifier, you can import the file into a specific location using the Location Manager upload page.

## Sample file
Click [here](./CrossSectionSample.csv) to view/download a sample file.

```
AQUARIUS Cross-Section CSV v1.0
 
Location: RKB002HK
StartDate: 2001-05-08T14:32:15+07:00
EndDate: 2001-05-08T17:12:45+07:00
Party: Joe Fieldtech
Channel: Right overflow
RelativeLocation: At the Gage
Stage: 12.2
Unit: ft
StartBank: Left bank
Comment: Used the big willow tree at the river bank as our anchor point.
 
Distance, Elevation, Comment
0, 7.467,
19.1, 6.909, "some comment"
44.8, 6.3, "yet, another, comment"
70.1, 5.356, another comment
82.4, 5.287,
```

## Supported CSV format

The supported CSV file has the following text rules:
- UTF-8 encoding is assumed. The UTF-8 byte-order-mark of (`0xEF, 0xBB, 0xBF`) at the start of the file is completely optional.
- Completely blank lines are ignored.
- The header line (line 14 in the sample file above) can be repeated any number of times, and will always be ignored. The field names in the header line must match the English field names listed in the table below.
- Leading/trailing whitespace is allowed between fields.
- Multi-line text values are not supported. (ie. the **Comment** field must be a single line)

### Starting line

The first line must be `AQUARIUS Cross-Section CSV v1.0`, with no leading/trailing whitespace.

If the first line does not match the expected pattern, the plugin will not attempt to parse the file.

### Field lines

Lines that follow the `<FieldName> : <FieldValue>` pattern will not be parsed as CSV data rows, but will set some field values which apply to the entire cross-section measurement.

- Leading/trailing whitespace is ignored
- The whitespace separating `<FieldName>`, the colon `:`, and `<FieldValue>` is ignored.
- The `<FieldName>`s are case-insensitive.

| Field name | Data type | Required? | Description |
| --- | --- | --- | --- |
| **Location** | string | Y | The location identifier for this measurement. |
| **StartDate** | [Timestamp](#timestamps) | Y | The starting time of the survey. |
| **EndDate** | [Timestamp](#timestamps) | Y | The ending time of the survey. |
| **Party** | string | Y | The party performing the survey. |
| **Channel** | string | N | The name of the channel at the location. If omitted, a value of `Main` will be used. |
| **RelativeLocation** | string | N | A description of where the measurement was taken. If omitted, a value of `At the control` will be used. |
| **Stage** | numeric | Y | The stage height at the time of the survey.  |
| **Unit** | string | Y | The stage height units. |
| **StartBank** | string | Y | The starting bank of the survey, The value must start with `Left`, `LEW`, `Right`, or `REW`. |
| **Comment** | string | Y | A comment to apply to the entire survey. If no comment is needed, leave the value empty. |

### CSV Header lines

A header line does not need to appear at all in the CSV file.
But if it does exist, it must exactly match line 14 in the [example file](#example-file) below.
The header line must be the 3 column names, listed in the order below, separated by commas. Whitespace between column names is ignored.

### Column definitions for CSV data rows

| ColumnName | Data type | Required? | Description |
| --- | --- | --- | --- |
| **Distance** | numeric | Y | The distance from the start bank. |
| **Elevation** | numeric | Y | The elevation relative to the stage. Positive values are deeper (below the water line). Negative values are above the water line. |
| **Comment** | string | N | An optional comment |

### Timestamps

Timestamps are specified in ISO 8601 format. Specifically the `"O"` (roundtrip) format for .NET DateTimeOffset values is used.

`yyyy-MM-ddTHH:mm:ss.fffffffzzz`

- The `T` character must separate the date and time portions.
- All seven fractional seconds digits are optional. This 100 nanosecond precision is the full resolution supported by AQUARIUS Time-Series.
- The time-zone (`zzz`) portion can either be an explicit offset in hours/minutes (`+04:00` or `-00:30`) or it can be the UTC indicator of the letter `Z`.
- These constraints ensure that the timestamps contain no ambiguity.

See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#Roundtrip for more details.
