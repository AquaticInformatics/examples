# PointZilla

`PointZilla` is a console tool for quickly appending points to a time-series in an AQTS 201x system.

Download the [latest PointZilla.exe release here](../../../../../../releases/latest)
 
Points can be specified from:
- Command line parameters (useful for appending a single point)
- Signal generators: linear, saw-tooth, square-wave, or sine-wave signals. Useful for just getting *something* into a time-series
- CSV files (including CSV exports from AQTS Springboard)
- Points retrieved live from other AQTS systems, including from legacy 3.X systems.
- `CMD.EXE`, `PowerShell` or `bash`: `PointZilla` works well from within any shell.

Basic time-series will append time/value pairs. Reflected time-series also support setting grade codes and/or qualifiers to each point.

Like its namesake, Godzilla, `PointZilla` can be somewhat awesome, a little scary, and even wreak some havoc every now and then.
- We don't recommend deploying either `PointZilla` or Godzilla in a production environment.
- Don't try to use `PointZilla` to migrate your data. System-wide data migration has many unexpected challenges.
- May contain traces of peanuts.

![Rawrrr!](./PointZilla.png "Rawwr!")

# Requirements

- `PointZilla` requires the .NET 4.7 runtime, which is pre-installed on all Windows 10 and Windows Server 2016 systems, and on nearly all up-to-date Windows 7 and Windows Server 2008 systems.
- `PointZilla` is a stand-alone executable. No other dependencies or installation required.
- An AQTS 2017.2+ system

# Examples

These examples will get you through most of the heavy lifting to get some points into your time-series.

### Command line option syntax

All command line options are case-insensitive, and support both common shell syntaxes: either `/Name=value` (for CMD.EXE) or `-Name=value` (for bash and PowerShell).

In addition, the `@options.txt` syntax is supported, to read options from a text file. You can mix and match individual `/name=value` and `@somefile.txt` on the same command line.

Try the `/help` option for a detailed list of options and their default values.

### Authentication credentials

The `/Server` option is required for all operations performed. The `/Username` and `/Password` options default to the stock "admin" credentials, but can be changed as needed.

### Use positional arguments to save typing

Certain frequently used options do not need to be specified using the `/name=value` or `-name=value` syntax.

The `/Command=`, `/TimeSeries=`, and `/CsvFile=` options can all omit their option name. `PointZilla` will be able to determine the appropriate option name from the command line context.

## Append *something* to a time-zeries

With only a server and a target time-series, `PointZilla` will used its built-in signal generator and append one day's worth of 1-minute values, as a sine wave between 1 and -1, starting at "right now".

```cmd
C:\> PointZilla /Server=myserver Stage.Label@MyLocation

15:02:36.118 INFO  - Generated 1440 SineWave points.
15:02:36.361 INFO  - Connected to myserver (2017.4.79.0)
15:02:36.538 INFO  - Appending 1440 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:02:39.493 INFO  - Appended 1440 points (deleting 0 points) in 3.0 seconds.
```

- The built-in signal generator supports `SineWave`, `SquareWave`, `SawTooth`, and `Linear` signal generation, with configurable amplitude, phase, offset, and period settings.
- Use the `/StartTime=yyyy-mm-ddThh:mm:ssZ` option to change where the generated points will start.

## Append a single point to a time-series

Need one specific value in a time-series? Just add that value to the command line.

This example appends the value 12.5, using the default timestamp of "right now".

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation 12.5

15:10:04.176 INFO  - Connected to myserver (2017.4.79.0)
15:10:04.313 INFO  - Appending 1 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:10:05.405 INFO  - Appended 1 points (deleting 0 points) in 1.1 seconds.
```

- You can add as many numeric values on the command line as needed.
- Each generated point will be spaced one `/PointInterval` duration apart (defaults to 1-minute)
- Use the `/StartTime=yyyy-mm-ddThh:mm:ssZ` option to change where the generated points will start.

## Append values from a CSV file

`PointZilla` can also read times, values, grade codes, and qualifiers from a CSV file.

All the CSV parsing options are configurable, but will default to values which match the CSV files exported from AQTS Springboard from 201x systems.

The `-csvFormat=` option supports two prefconfigured formats:

- `-csvFormat=NG` is equivalent to `-csvTimeField=1 -csvValueField=3 -csvGradeField=5 -csvQualifiersField=6 -csvSkipRows=0 -csvComment="#"`
- `-csvFormat=3X` is equivalent to `-csvTimeField=1 -csvValueField=2 -csvGradeField=3 -csvQualifiersField=0 -csvSkipRows=2 -csvTimeFormat="MM/dd/yyyy HH:mm:ss"`

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation Downloads/Stage.Historical@A001002.EntireRecord.csv

15:29:20.984 INFO  - Loaded 621444 points from 'Downloads/Stage.Historical@A001002.EntireRecord.csv'.
15:29:21.439 INFO  - Connected to myserver (2017.4.79.0)
15:29:21.767 INFO  - Appending 621444 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:29:40.086 INFO  - Appended 621444 points (deleting 0 points) in 18.3 seconds.
```

Parsing CSV files exported from AQTS 3.X systems requires a different CSV parsing configuration.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation Downloads/ExportedFrom3x.csv -csvFormat=3x

13:45:49.400 INFO  - Loaded 250 points from 'Downloads/ExportedFrom3x.csv'.
13:45:49.745 INFO  - Connected to myserver (2017.4.79.0)
13:45:49.944 INFO  - Appending 250 points to Stage.Label@MyLocation (ProcessorBasic) ...
13:45:51.143 INFO  - Appended 250 points (deleting 0 points) in 1.2 seconds.
```

## Realigning CSV points with the `/StartTime` value

When `/CsvRealign=true` is set, all the imported CSV rows will be realigned to the `/StartTime` option.

This option can be a useful technique to "stitch together" a simulated signal with special shapes at specific times.

## Creating missing time-series or locations (Use with caution!)

By default (the `/CreateMode=Never` option), trying to append points to a non-existent time-series will quickly return an error.

The `/CreateMode=Basic` and `/CreateMode=Reflected` options can be used to quickly create a missing time-series if necessary.
When time-series creation is enabled, a location will also be created if needed.

This mode is useful for testing, but is not recommended for production systems, since the default configurations chosen by PointZilla are likely not correct.

### Caveats about creating a time-series using PointZilla

There are a number of command line options which help you create a time-series correctly (see the "Time Series creation options" section in the `--help` page).

The basic rules for the setting created time-series properties are:
- Use reasonable defaults when possible.
- The parameter's default unit, interpolation type, and monitoring method are used as a starting point.
- No gap tolerance is configured by default.
- If you are copying a time-series from another AQTS system using the [`/SourceTimeSeries=`](#copying-points-from-another-time-series) option, copy as many of the source time-series properties as possible.
- Any command line options you set will override any automatically inferred defaults.

So where does this approach fall down? What are the scenarios where using PointZilla to create and copy a time-series won't give me an exact match of the original?
- PointZilla just copies the corrected points and uses those values as raw point values. You lose the entire correction history.
- PointZilla can't copy the gap tolerance or interpolation type from an AQTS 3.X system. If you need a different value, you'll need to set a `/GapTolerance=` or `/InterpolationType=` command line option explicitly.

### I created my time-series incorrectly, oh no! What do I do now?

[LocationDeleter](https://github.com/AquaticInformatics/examples/blob/master/TimeSeries/PublicApis/SdkExamples/LocationDeleter/Readme.md) (aka. "DeleteZilla") is your friend here.

If your PointZilla command-line creates a time-series incorrectly, just use `LocationDeleter` in [Time-Series Deletion Mode](https://github.com/AquaticInformatics/examples/blob/master/TimeSeries/PublicApis/SdkExamples/LocationDeleter/Readme.md#deleting-time-series) to delete the borked time-series and try again.

## Appending grades and qualifiers

When the target time-series is a reflected time-series, any grade codes or qualifiers imported from CSV rows or manually set via the `/GradeCode` or `/Qualifiers` options will be appended along with the core timestamp and values.

When specifying point values on the command line, you must specify the `/GradeCode` or `/Qualifiers` option before specifying the numeric value.

Grade codes and qualifiers will not be appended to basic time-series.

## Copying points from another time-series

When the `/SourceTimeSeries` option is set, the corrected point values from the source time-series will be copied to the target `/TimeSeries`.

Unlike the target time-series, which are restricted to basic or reflected time-series types, a source time-series can be of any type.

The `/SourceQueryFrom` and `/SourceQueryTo` options can be used to restrict the range of points copied. When omitted, the entire record will be copied.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation -sourceTimeSeries=Stage.Working@OtherLocation

15:18:32.711 INFO  - Connected to myserver (2017.4.79.0)
15:18:35.255 INFO  - Loaded 1440 points from Stage.Working@OtherLocation
15:18:35.356 INFO  - Connected to myserver (2017.4.79.0)
15:18:35.442 INFO  - Appending 1440 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:18:37.339 INFO  - Appended 1440 points (deleting 0 points) in 1.9 seconds.
```

## Copying points from another time-series on another AQTS system

The `/SourceTimeSeries=[otherserver]parameter.label@location` syntax can be used to copy time-series points from a completely separate AQTS system.

If different credentials are required for the other server, use the `[otherserver:username:password]parameter.label@location` syntax.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation -sourcetimeseries="[otherserver]Stage.Working@OtherLocation"

13:31:57.829 INFO  - Connected to otherserver (3.10.905.0)
13:31:58.501 INFO  - Loaded 250 points from Stage.Working@OtherLocation
13:31:58.658 INFO  - Connected to myserver (2017.4.79.0)
13:31:58.944 INFO  - Appending 250 points to Stage.Label@MyLocation (ProcessorBasic) ...
13:32:00.148 INFO  - Appended 0 points (deleting 0 points) in 1.2 seconds.
```

The source time-series system can be any AQTS system as far back as AQUARIUS Time-Series 3.8.

## Comparing the points in two different time-series

Use the `/SaveCsvPath=` option to save the extracted points to a CSV file, and then use standard text differencing tools to see if anything is different.

Here is a bash script which compares the saved CSV output of two time-series. This only compares the corrected points, but that is usually a good indicator of "sameness".

```sh
$ ./PointZilla.exe -saveCsvPath=system1 -sourceTimeSeries="[old3xServer]Stage.Primary@Location1"
$ ./PointZilla.exe -saveCsvPath=system2 -sourceTimeSeries="[newNgServer]Stage.Primary@Location1"
$ diff system1/Stage.Primary@Location1.EntireRecord.csv system2/Stage.Primary@Location1.EntireRecord.csv && echo "Time-series are identical." || echo "Nope, they are different"
```

## Deleting all points in a time-series

The `DeleteAllPoints` command can be used to delete the entire record of point values from a time-series.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation deleteallpoints

15:27:17.220 INFO  - Connected to myserver (2017.4.79.0)
15:27:17.437 INFO  - Deleting all existing points from Stage.Label@MyLocation (ProcessorBasic) ...
15:27:21.456 INFO  - Appended 0 points (deleting 622884 points) in 4.0 seconds.
```

With great power ... yada yada yada. Please don't wipe out your production data with this command.

## Deleting a range of points in a time-series

You can delete a range of points in a basic or reflected time-series by:
- specifying the `/NumberOfPeriods=0` option to generate no new points
- specifying the `/TimeRange=startTime/endTime` option to define the exact time range to be replaced with no points at all
- specifying either the `/Command=OverwriteAppend` option for basic time-series or `/Command=Reflected` for reflected time-series.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@Location -TimeRange=2018-04-25T00:00:00Z/2018-04-29T00:00:00Z -numberofperiods=0 overwriteappend
17:02:23.301 INFO  - Generated 0 SineWave points.
17:02:23.889 INFO  - Connected to myserver (2018.1.98.0)
17:02:24.076 INFO  - Appending 0 points within TimeRange=2018-04-25T00:00:00Z/2018-04-29T00:00:00Z to Stage.Label@Location (ProcessorBasic) ...
17:02:24.719 INFO  - Appended 0 points (deleting 1440 points) in 0.6 seconds.
```

## Command line options

Like `curl`, the `PointZilla` tool has dozens of command line options, which can be a bit overwhelming. Fortunately, you'll rarely need to use all the options at once.

Try the `/Help` option to see the entire list of supported options and read the [wiki for the @optionsFile syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options).

```
Append points to an AQTS time-series.

usage: PointZilla [-option=value] [@optionsFile] [command] [identifierOrGuid] [value] [csvFile] ...

Supported -option=value settings (/option=value works too):

  -Server                   AQTS server name
  -Username                 AQTS username [default: admin]
  -Password                 AQTS password [default: admin]
  -Wait                     Wait for the append request to complete [default: True]
  -AppendTimeout            Timeout period for append completion, in .NET TimeSpan format.
  -BatchSize                Maximum number of points to send in a single append request [default: 500000]
  
  ========================= Time-series options:
  -TimeSeries               Target time-series identifier or unique ID
  -TimeRange                Time-range for overwrite in ISO8061/ISO8601 (defaults to start/end points)
  -Command                  Append operation to perform.  One of Auto, Append, OverwriteAppend, Reflected, DeleteAllPoints. [default: Auto]
  -GradeCode                Optional grade code for all appended points
  -Qualifiers               Optional qualifier list for all appended points
  
  ========================= Time-series creation options:
  -CreateMode               Mode for creating missing time-series. One of Never, Basic, Reflected. [default: Never]
  -GapTolerance             Gap tolerance for newly-created time-series. [default: "MaxDuration"]
  -UtcOffset                UTC offset for any created time-series or location. [default: Use system timezone]
  -Unit                     Time-series unit
  -InterpolationType        Time-series interpolation type. One of InstantaneousValues, PrecedingConstant, PrecedingTotals, InstantaneousTotals, DiscreteValues, SucceedingConstant.
  -Publish                  Publish flag. [default: False]
  -Description              Time-series description [default: Created by PointZilla]
  -Comment                  Time-series comment
  -Method                   Time-series monitoring method
  -ComputationIdentifier    Time-series computation identifier
  -ComputationPeriodIdentifier Time-series computation period identifier
  -SubLocationIdentifier    Time-series sub-location identifier
  -TimeSeriesType           Time-series type. One of Unknown, ProcessorBasic, ProcessorDerived, External, Reflected.
  -ExtendedAttributeValues  Extended attribute values in UPPERCASE_COLUMN_NAME@UPPERCASE_TABLE_NAME=value syntax. Can be set multiple times.
  
  ========================= Copy points from another time-series:
  -SourceTimeSeries         Source time-series to copy. Prefix with [server2] or [server2:username2:password2] to copy from another server
  -SourceQueryFrom          Start time of extracted points in ISO8601 format.
  -SourceQueryTo            End time of extracted points
  
  ========================= Point-generator options:
  -StartTime                Start time of generated points, in ISO8601 format. [default: the current time]
  -PointInterval            Interval between generated points, in .NET TimeSpan format. [default: 00:01:00]
  -NumberOfPoints           Number of points to generate. If 0, use NumberOfPeriods [default: 0]
  -NumberOfPeriods          Number of waveform periods to generate. [default: 1]
  -WaveformType             Waveform to generate. One of Linear, SawTooth, SineWave, SquareWave. [default: SineWave]
  -WaveformOffset           Offset the generated waveform by this constant. [default: 0]
  -WaveformPhase            Phase within one waveform period [default: 0]
  -WaveformScalar           Scale the waveform by this amount [default: 1]
  -WaveformPeriod           Waveform period before repeating [default: 1440]
  
  ========================= CSV parsing options:
  -CSV                      Parse the CSV file
  -CsvTimeField             CSV column index for timestamps [default: 1]
  -CsvValueField            CSV column index for values [default: 3]
  -CsvGradeField            CSV column index for grade codes [default: 5]
  -CsvQualifiersField       CSV column index for qualifiers [default: 6]
  -CsvTimeFormat            Format of CSV time fields (defaults to ISO8601)
  -CsvComment               CSV comment lines begin with this prefix [default: #]
  -CsvSkipRows              Number of CSV rows to skip before parsing [default: 0]
  -CsvIgnoreInvalidRows     Ignore CSV rows that can't be parsed [default: True]
  -CsvRealign               Realign imported CSV points to the /StartTime value [default: False]
  -CsvRemoveDuplicatePoints Remove duplicate points in the CSV before appending. [default: True]
  -CsvFormat                Shortcut for known CSV formats. One of 'NG', '3X', or 'PointZilla'. [default: NG]
  
  ========================= CSV saving options:
  -SaveCsvPath              When set, saves the extracted/generated points to a CSV file. If only a directory is specified, an appropriate filename will be generated.
  -StopAfterSavingCsv       When true, stop after saving a CSV file, before appending any points. [default: False]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```
 
