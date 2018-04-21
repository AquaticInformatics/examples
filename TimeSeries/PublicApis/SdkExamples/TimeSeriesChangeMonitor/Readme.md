# TimeSeriesChangeMonitor

Download the latest release [from the releases page](../../../../../../releases/latest).

The `TimeSeriesChangeMonitor.exe` console utility allows you easily monitor how quickly changes in your AQTS time-series become available for external consumption via Publish API.

- It is a single .EXE, so nothing to install.
- Requires the .NET 4.7 runtime installed (true for most up-to-date Windows systems)
- Allows you fine tune all the `/GetTimeSeriesDescriptionList` request parameters, to precisely focus on only the changes you care about.
- Can run forever (until you type Ctrl-C), or can exit after a certain number of changes are detected.
- Displays polling response summary statistics on exit, to help diagnose overall system performance.

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
  -ChangesSinceTime            The starting changes-since time. Defaults to 'right now'
  -PollInterval                The polling interval [default: 0:00:00:10]
  -MaximumChangeCount          When greater than 0, exit after detecting this many changed time-series. [default: 0]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```

