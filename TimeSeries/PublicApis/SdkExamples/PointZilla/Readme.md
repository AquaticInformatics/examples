# PointZilla

`PointZilla` is a console tool for quickly appending points to a time-series in an AQTS 201x system.
 
Points can be specified from:
- Command line parameters (useful for appending a single point)
- Function generators: linear, saw-tooth, or sine-wave signals. Useful for just getting *something* into a time-series
- CSV files (including CSV exports from AQTS Springboard)
- Points retrieved live from other AQTS systems, including from legacy 3.X systems.
- `CMD.EXE`, `PowerShell` or `bash`: `PointZilla` works well from within any shell.

Basic time-series will append time/value pairs. Reflected time-series also support setting grade codes and/or qualifiers to each point.

Like its namesake, Godzilla, `PointZilla` can be somewhat awesome, a little scary, and even wreak some havoc every now and then. We don't recommend deploying either `PointZilla` or Godzilla in a production environment.

![Rawrrr!](./PointZilla.png "Rawwr!")

# Requirements

- `PointZilla` requires the .NET 4.7 runtime to be installed.
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

All the CSV parsing options are configurable, but will default to values which match the CSV files exported from AQTS Springboard.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation Downloads/Stage.Historical@A001002.EntireRecord.csv

15:29:20.984 INFO  - Loaded 621444 points from 'Downloads/Stage.Historical@A001002.EntireRecord.csv'.
15:29:21.439 INFO  - Connected to myserver (2017.4.79.0)
15:29:21.767 INFO  - Appending 621444 points to Stage.Label@MyLocation (ProcessorBasic) ...
15:29:40.086 INFO  - Appended 621444 points (deleting 0 points) in 18.3 seconds.
```

Parsing CSV files exported from AQTS 3.X systems requires a different CSV parsing configuration.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation Downloads/ExportedFrom3x.csv -csvTimeField=1 -csvValueField=2 -csvGradeField=3 -csvQualifiersField=0 -csvSkipRows=2 -csvTimeFormat="MM/dd/yyyy HH:mm:ss"

13:45:49.400 INFO  - Loaded 250 points from 'Downloads/ExportedFrom3x.csv'.
13:45:49.745 INFO  - Connected to myserver (2017.4.79.0)
13:45:49.944 INFO  - Appending 250 points to Stage.Label@MyLocation (ProcessorBasic) ...
13:45:51.143 INFO  - Appended 250 points (deleting 0 points) in 1.2 seconds.
```

## Realigning CSV points with the `/StartTime` value

When `/CsvRealign=true` is set, all the imported CSV rows will be realigned to the `/StartTime` option.

This option can be a useful technique to "stitch together" a simulated signal with special shapes at specific times.

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
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation -sourcetimeseries=[otherserver]Stage.Working@OtherLocation

13:31:57.829 INFO  - Connected to otherserver (3.10.905.0)
13:31:58.501 INFO  - Loaded 250 points from Stage.Working@OtherLocation
13:31:58.658 INFO  - Connected to myserver (2017.4.79.0)
13:31:58.944 INFO  - Appending 250 points to Stage.Label@MyLocation (ProcessorBasic) ...
13:32:00.148 INFO  - Appended 0 points (deleting 0 points) in 1.2 seconds.
```

The source time-series system can be any AQTS system as far back as AQUARIUS Time-Series 3.8.

## Deleting all points

The `DeleteAllPoints` command can be used to delete the entire record of point values from a time-series.

```sh
$ ./PointZilla.exe -server=myserver Stage.Label@MyLocation deleteallpoints

15:27:17.220 INFO  - Connected to myserver (2017.4.79.0)
15:27:17.437 INFO  - Deleting all existing points from Stage.Label@MyLocation (ProcessorBasic) ...
15:27:21.456 INFO  - Appended 0 points (deleting 622884 points) in 4.0 seconds.
```

With great power ... yada yada yada. Please don't wipe out your production data with this command.

## Command line options

Like `curl`, the `PointZilla` tool has dozens of command line options, which can be a bit overwhelming. Fortunately, you'll rarely need to use all the options at once.

Try the `/Help` option to see the entire list of supported options.
 