# PointZilla

`PointZilla` is a console tool for quickly appending points to a time-series in an AQTS 201x system.
 
Points can be specified from:
- Command line parameters (useful for appending a single point)
- Function generators: linear, saw-tooth, or sine-wave signals. Useful for just getting *something* into a time-series
- CSV files (including CSV exports from AQTS Springboard)
- Points retrieved live from other AQTS systems, including from legacy 3.X systems.
- `CMD.EXE`, `PowerShell` or `bash`: `PointZilla` works well from within any shell.

Basic time-series will append time/value pairs. Reflected time-series also support setting grade codes and/or qualifiers to each point.

![Point](./PointZilla.png)

# Requirements

- `PointZilla` requires the .NET 4.7 runtime to be installed.
- `PointZilla` is a stand-alone executable. No other dependencies or installation required.
- An AQTS 2017.2+ system

# Examples

These examples will get you through most of the heavy lifting to get some points into your time-series.

## Append *something* to a time-zeries

With only a server and a target time-series, `PointZilla` will used its built-in function generator and append one day's worth of 1-minute values, as a sine wave between 1 and -1, starting at "right now".

```cmd
C:\> PointZilla /Server=myserver Stage.Label@MyLocation

15:02:36.118 INFO  - Generated 1440 SineWave points.
15:02:36.361 INFO  - Connected to myserver (2017.4.79.0)
15:02:36.538 INFO  - Appending 1440 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:02:39.493 INFO  - Appended 1440 points (deleting 0 points) in 3.0 seconds.
```

- The built-in function generator supports `SineWave`, `SawTooth`, and `Linear` signal generation, with configurable amplitude, phase, offset, and period settings.
- Use the `/StartTime=yyyy-mm-ddThh:mm:ssZ` option to change where the generated points will start.

## Append a single point to a time-series

Need one specific value in a time-series? Just add that value to the command line.

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

`PointZilla` can also read times, values, grade codes, and qualifiers from a CSV file. All the CSV parsing options are configurable, but default to values which match the CSV files exported from AQTS Springboard.

```sh
$ ./PointZilla.exe -server=doug-vm2012r2 Stage.Fake2@SchmidtKits Downloads/Stage.Historical@A001002.EntireRecord.csv

15:29:20.984 INFO  - Loaded 621444 points from 'Downloads/Stage.Historical@A001002.EntireRecord.csv'.
15:29:21.439 INFO  - Connected to myserver (2017.4.79.0)
15:29:21.767 INFO  - Appending 621444 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:29:40.086 INFO  - Appended 621444 points (deleting 0 points) in 18.3 seconds.
```

## Command line options

Like `curl`, the `PointZilla` tool has dozens of command line options, which can be a bit overwhelming. Fortunately, you'll rarely need to use all the options at once.

Rather than list them all at once, we'll list the related options in sections.

## Authenticating


of them at once.The are tons of options
## Rough notes

/Server=
/Username=(def. admin)
/Password=(def. admin)

/Wait=true|false (def. true)
/AppendTimeout=TimeSpan? (def. null)

/TimeRange=Interval? (for overwrite/reflected append. def. null = use start/end of generated points, "MinInstant" and "MaxInstant" are supported)

/NumberOfPoints=0 (0 means derive from periods)
/NumberOfPeriods=1
/StartTime=Instant? (def null = UtcNow)
/PointInterval=TimeSpan (def 1 minute)
/FunctionType=Linear|SawTooth|SineWave (def. Sine)
/FunctionOffset=0
/FunctionPhase=0
/FunctionScalar=1.0
/FunctionPeriod=1440

/Csv=file
/CsvTimeField=(def. 1) - Field index of 0 means "don't use". number:Format. Assume ISO8601 if format omitted.
/CsvTimeFormat=ISO8601
/CsvValueField=(def. 3)
/CsvGradeField=(def. 5)
/CsvQualifierField=(def. 6)
/CsvComment=#
/CsvSkipLines=int (def. 0)
/CsvIgnoreInvalidRows=true
/CsvRealign=false (true will adjust to /StartTime)

Multiple /CSV=file will parse multiple files (useful for combining points)
Skip any CSV row that doesn't parse (If a time doesn't parse, or a value doesn't parse, then skip it. This will skip the header by default, even when /CsvSkipLines is zero)
 
/CreateMode=None|Basic|Reflected (def. None)
/Command=Auto|Append|Overwrite|Reflected (def. Auto = Append or Reflected, depending on time-series type.)
/Grade=gradecode (apply to all points)
/Qualifier=list (apply to all points)
/TimeSeries=identifierOrGuid
/SourceTimeSeries=[server:[username:password:]]identifierOrGuid

PointZilla /options [command] [identifierOrGuid] [value] [csvFile]

- Use function generator if no CSV or explicit point values are defined
- Repeat CSV points by NumberOfPeriods
- 