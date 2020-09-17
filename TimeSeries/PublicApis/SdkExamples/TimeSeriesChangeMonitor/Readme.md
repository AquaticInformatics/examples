# TimeSeriesChangeMonitor

Download the latest release [from the releases page](../../../../../../releases/latest).

The `TimeSeriesChangeMonitor.exe` console utility allows you easily monitor how quickly changes in your AQTS time-series become available for external consumption via Publish API.

- It is a single .EXE, so no installer is needed. Just download it and run it.
- Requires the .NET 4.7 runtime installed (true for most up-to-date Windows systems)
- Allows you fine tune all the `/GetTimeSeriesDescriptionList` request parameters, to precisely focus on only the changes you care about.
- Works from `cmd.exe`, bash, or PowerShell.
- Can run forever (until you type Ctrl-C), or can exit after a certain number of changes are detected.
- Displays polling response summary statistics on exit, to help diagnose overall system performance.
- All the output is also logged to `TimeSeriesChangeMonitor.log` in the same folder as the EXE.

### Command line option syntax

All command line options are case-insensitive, and support both common shell syntaxes: either `/Name=value` (for CMD.EXE) or `-Name=value` (for bash and PowerShell).

In addition, the [`@options.txt` syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) is supported, to read options from a text file. You can mix and match individual `/name=value` and `@somefile.txt` on the same command line.

Try the `/help` option for a detailed list of options and their default values.

## Integrating with other programs

The `TimeSeriesChangeMonitor.exe` tool can be used as the first step of a custom notification workflow.

The basic idea is to configure `TimeSeriesChangeMonitor` with enough options to only detect the conditions you care about, and then use either the `/MaximumChangeCount=` or `/DetectedChangesCsv=` option to cause the tool to exit once the changes have been detected.

Here is an awful example of nagging a colleague whenever rain is detected in the location he is responsible for.

```sh
#/bin/nash

# Nag Dave with a text every time it's raining

while true
  # Wait until a new rain data point arrives at Dave's location
  TimeSeriesChangeMonitor.exe -server=myappserver -LocationIdentifier="DaveLocation" -Parameter="Precip Increm" -ChangeEventType=Data -MaximumChangeCount=1

  # Notify Dave. Dave hates me.
  SendTextToDave.exe "Hey Dave, it's raining!"
done
```

### The `-DetectedChangesCsv=pathToCsv` CSV output format

The `-DetectedChangesCsv=` option will write all detected changes to a CSV file, which can be processed by your integration to take action on the changes detected.

```
TimeSeriesUniqueId,EventType,TimeSeriesIdentifier,FirstPointChangedUtc,LastMatchedTimeUtc
2ad74bbf1c3746e98e9d8df75143a51b,FullSync,Stage.Telemetry@G8140001,,
```

| Column | Description |
|---|---|
| **TimeSeriesUniqueId** | 32-character time-series ID. Can be used in subsequent API calls to fetch more relevant info. |
| **EventType** | One of:<br/>- `DataChanged` - One or more points have changed.<br/>- `AttributeChanged` - Some metadata (eg. Label, comment) has changed.<br/>- `Deleted` - The time-series was deleted from the AQTS system.<br/>- `FullSync` - The event is not a change from the last time, but is a full sync event. |
| **TimeSeriesIdentifier** | The text identifier of the time-series, in `Parameter.Label@Location` format. |
| **FirstPointChangedUtc** | When **EventType** is `DataChanged`, this column will have the time of the first point changed in the time-series. |
| **LastMatchedTimeUtc** | When **EventType** is `Deleted`, this column will have the time the series was deleted. |

## General help screen

The `/help` option shows the following screen:

```
Monitor time-series changes in an AQTS time-series.

usage: TimeSeriesChangeMonitor [-option=value] [@optionsFile] [location] [timeSeriesIdentifierOrGuid] ...

Supported -option=value settings (/option=value works too):

  -Server                      AQTS server name [default: localhost]
  -Username                    AQTS username [default: admin]
  -Password                    AQTS password [default: admin]
  -LocationIdentifier          Optional location filter.
  -Publish                     Optional publish filter.
  -ChangeEventType             Optional change event type filter. One of Data, Attribute
  -Parameter                   Optional parameter filter.
  -ComputationIdentifier       Optional computation filter.
  -ComputationPeriodIdentifier Optional computation period filter.
  -ExtendedFilters             Optional extended attribute filter in Name=Value format. Can be set multiple times.
  -TimeSeries                  Optional time-series to monitor. Can be set multiple times.
  -ChangesSinceTime            The starting changes-since time in ISO 8601 format. Defaults to 'right now'
  -ForceFullSync               When set, force the /ChangesSinceTime value to sync all matching time-series. [default: False]
  -PollInterval                The polling interval in ISO 8601 Duration format. [default: PT5M]
  -MaximumPollCount            When set, exit after this many polling intervals.
  -MaximumChangeCount          When set, exit after detecting this many changed time-series.
  -AllowQuickPolling           Allows very quick polling. Good for testing, bad for production. [default: False]
  -SavedChangesSinceJson       Loads the /ChangesSinceTime value from this JSON file.
  -DetectedChangesCsv          When set, save all detected changes to this CSV file and exit.

ISO 8601 timestamps use a yyyy'-'mm'-'dd'T'HH':'mm':'ss'.'fffffffzzz format.

  The 7 fractional seconds digits are optional.
  The zzz timezone can be 'Z' for UTC, or +HH:MM, or -HH:MM

  Eg: 2017-04-01T00:00:00Z represents April 1st, 2017 in UTC.

ISO 8601 durations use a 'PT'[nnH][nnM][nnS] format.

  Only the required components are needed.

  Eg: PT5M represents 5 minutes.
      PT90S represents 90 seconds (1.5 minutes)

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```

## All the `/GetTimeSeriesDescriptionList` request filters are supported

All of the request filters of the `GET /Publish/v2/GetTimeSeriesDescriptionList` request can be configured.

- When no request filters are set, the command will monitor all the time-series in your AQTS system.
- If you set multiple `/TimeSeries=` options, the tool will try to use the `LocationIdentifier`, `Parameter`, and/or `Publish` filters if all the monitored time-series have these values in common. This optimization will give optimum polling response times. 

## Polling for changes quickly

A large production system should not need to be polled for changes more frequently than about once every 5 minutes, and this utility enforces that limit by default.

Polling more frequently is not super harmful, but putting too much pressure on the database is never a great idea.

```sh
$ ./TimeSeriesChangeMonitor.exe -server=doug-vm2012r2 -pollinterval=pt1s
00:08:30.509 INFO  - Connecting TimeSeriesChangeMonitor v1.0.0.0 to doug-vm2012r2 ...
00:08:31.109 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
00:08:31.192 ERROR - Polling more quickly than every 5 minutes is not enabled.
```

But on test systems, or if you are simply impatient, you can set the `/AllowQuickPolling=True` option and poll more frequently. A `WARN` message will be logged to remind you that frequent polling on production systems is not recommended.

```sh
$ ./TimeSeriesChangeMonitor.exe -server=doug-vm2012r2 -pollinterval=pt1s -allowquickpolling=true
00:09:18.957 INFO  - Connecting TimeSeriesChangeMonitor v1.0.0.0 to doug-vm2012r2 ...
00:09:19.580 INFO  - Connected to doug-vm2012r2 (2018.1.98.0)
00:09:19.634 WARN  - Polling more quickly than every 5 minutes is not recommended for production systems.
00:09:19.643 INFO  - Monitoring all locations for changes in any time-series every 1 second
00:09:19.643 INFO  - Press Ctrl+C or Ctrl+Break to exit.
```
