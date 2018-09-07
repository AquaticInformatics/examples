# SOS Exporter

`SosExporter.exe` is a .NET console utility which can export changes from AQTS 201x time-series to an OGC SOS server as sensor observations (suitable for consumption by national aggregation systems like [LAWA](https://www.lawa.org.nz/)).

- `SosExporter.exe` is a single EXE, with no external dependencies other than the .NET 4.7 runtime. It can be easily deployed from any Windows system.
- Reasonable defaults are assumed. By default, all AQTS time-series with Publish=true will be exported.
- All options can be configured from the command line and/or from configuration files.
- The exporter works in "incremental" mode when possible, exporting only newly appended points as they appear.
- When an incremental export is not possible, the entire time-series record is exported, according to the `-MaximumPointDays` configuration.
- Changes to the export configuration which break incremental exports will automatically be detected and a full resync will be performed. 
- The `SosExporter.log` file will contain the output of the most recent export cycle.

Try the `/help` option for more details.

## Requirements

- .NET 4.7 runtime (any up-to-date Windows system will contain this runtime).
- Credentials for an AQTS app server and a SOS OGC server.

## Installation

No installation is required. `SosExporter.exe` is a single executable which can be run from any folder.

## Configuration

The exporter is highly configurable via command line options and/or configuration files. Any changes which affect the time-series being exported will force a full resync.

Command-line options use either the `-option=value` or `/Option=value` syntax. The "option" name is case-insensitive. This allows the exporter to be run from any popular command shell: CMD.EXE, Powershell, or bash.

The recommended approach is to create a configuration file, a simple text file with one configuration option per line.

You can run the exporter with the values from a configuration file by using the @filename syntax. Just add the filename as a command line option immediately preceeded by an at-sign.

```cmd
C:\> SosExporter @sosconfig.txt
```

Consider the following 15-line file named "sosconfig.txt":

```
# Set the server credentials
-AquariusServer=myappserver
-AquariusUsername=myaqtsuser
-AquariusPassword=abc123
-SosServer=http://mysosserver/sos-webapp
-SosUsername=mysosuser
-SosPassword=xyz456

# Only export the time-series with an extended attribute named "SOS" with a value of "Export"
# Since the default is -Publish=true, we need to disable that filter
-Publish=
-ExtendedFilters=SOS=Export

# Only export approved points
-Approvals=Approved
```

Note that:
- Each line in the file is treated as a command line option. Both the `-option=value` and `/option=value` syntaxes are valid.
- Blank lines and leading/trailing whitespace are ignored.
- Comment lines begin with a # or // marker.

You can also combine @filename and `-option=value` command line arguments any number of times, in any order.

The following command will run everything from the config file, but in dry-run mode.

```cmd
C:\> SosExporter @sosconfig.txt /DryRun=True
```

## Full resync vs. incremental exports

The first time the exporter is run, it will need to perform a full resync with the SOS server. This will involve:
- Deleting all sensors and observations from the SOS server.
- Re-creating a new sensor for every exported AQTS time-series.
- Exporting the last `-MaximumPointDays` days worth of points from AQTS. The number of days exported depends upon the frequency of the time-series and is configurable.

A full resync cycle can take many hours, depending on the number of time-series configured for export, and the number of points exported. We have observed the exporter taking about an hour to export 5000 time-series with a combined total of 1 million observed points. Your mileage may vary.

Incremental exports are much, much faster, often within minutes or even seconds, since they can leverage the super-efficient "Changes Since" polling mechanism of the AQUARIUS Publish API. An incremental export cycle on the same 5000 time-series system with no changes will finish in under 5 seconds.

The exporter works very hard to perform an incremental export whenever possible. When an incremental export is not possible, the exporter performs a slow full resync, and hopes that an incremental export will be possible on the next cycle (which should be true if no configuration changes are made).

The following events will force a full resync:
- Switching the AQTS database (this is a rare thing for production systems)
- More than a day has elapsed since the last export cycle. The Publish API "Changes Since" mechanism requires that you poll the system more frequently than once a day, otherwise it says "Your changes since token has expired" and the only option is a full resync.
- Specifying the `-ForceResync=true` command line option
- Changing any of the configuration values which affect the time-series being monitored. These are documented in the `-help` screen as `Changes will trigger a full resync:`
  - AQTS or SOS server credentials
  - The `GET /Publish/v2/GetTimeSeriesUniqueIdList` settings (like `-Publish` or `-ExtendedFilters`)
  - The inclusion/exclusion filters for time-series identifiers
  - The inclusion/exclusion filters for time-series points based on approvals, grades, and/or qualifiers
  - The `-MaximumPointDays` configuration, which sets the maximum retrieval time based on the time-series frequency.
  
## Run the exporter on a schedule

The SOS exporter utility will perform one export cycle and then exit. It can be run on a schedule from any standard scheduler like the Windows Task Scheduler.

Many schedulers (including the Windows Task Scheduler) have a setting which says "kill the task if it takes too long to run". You will need to set this setting large enough to handle a full resync cycle without killing the export task, otherwise it will never be able complete the initial slow sync and start running in the much faster incremental export on subsequent cycles.

## Dry-run mode

Adding the `/DryRun=true` option will cause the exporter to perform a dry run, logging only the SOS export operations which would have been performed, but not making any changes.
This is useful for debugging configuration changes before deploying them to a production environment.

## Advanced filtering of exported time-series and values

### Supports all /GetTimeSeriesUniqueIdList filtering options

All of the optional `/GetTimeSeriesUniqueIdList` request parameters can be specified to perform efficient filtering of changes detected to AQTS time-series.

- `/LocationIdentifier=pattern` to filter by location identifier (with * used for partial matching)
- `/Publish=True` or`/Publish=False` to filter by the time-series Publish flag. Use `/Publish=` (with nothing after the equals sign) to disable the filter.
- `/ChangeEventType=Data` or `/ChangeEventType=Attribute` to filter by data changes or metadata changes.
- `/Parameter=identifier` to filter by a specific parameter
- `/ComputationIdentifier=identifier` to filter by a computation type
- `/ComputationPeriodIdentifier=identifier` to filter by a computation period
- `/ExtendedFilters=Name=Value` to filter by an extended attribute value.

By default, the `/Publish=true` option is set, but this can be changed.

If your AQTS system uses extended attributes to control exporting to LAWA, remember to also specify `/Publish=` (with nothing after the equals sign) to turn off the Publish filter.

Eg. If you only export to LAWA when the custom time-series extended attribute named "ExportToLAWA" equals the string "Yes", then your command line options should be:

```cmd
SosExporter /Publish= /ExtendedFilters=ExportToLAWA=Yes (other options ...)
```

See the Publish API Reference Guide for more details on how these request parameters operate.

### Filter time-series by regular expression

You can specify [.NET regular expressions](http://regexstorm.net/tester?p=%40LOC%5Cd%2B&i=Stage.Logger%40LOC5%0aStage.Logger%40LOCATION%0aBattery%20Voltage.Logger@somelocation%0aStage.Telemetry%40LOC77) to filter the list of exported time-series by patterns in the time-series identifier or the time-series description.

Use the `/TimeSeries=regex` option to filter the changed time-series by identifier.

Use the `/TimeSeriesDescription=regex` option to filter the changed time-series by description.

The `regex` regular expression just needs to match a portion of the text property being filtered. So `/TimeSeries=@L` will match all time-series in a location starting with a captial "L".

- More than one `/TimeSeries=regex` or `/TimeSeriesDescription=regex` filter option can be specified on the command line or in a configuration file.
- Each filter operates as either an **inclusion** or **exclusion** filter.
- A filter is assumed to be an **inclusion** filter, unless the `regex` value begins with a minus-sign.
- If no **inclusion** filters are specified, all time-series will be included by default.

This pattern of inclusion and exclusion filtering can be used to precisely control which time-series are exported.

Eg. Export all the time-series in locations starting with "NQ" or "WA", but exclude any time-series with a description ending with "offline"

```cmd
SosExporter /TimeSeries=@NQ /TimeSeries=@WA /TimeSeriesDescription="-offline$" (other options ...)
```

### Filter individual points by approval level, grade code, or qualifiers

Use the `/Approvals=level`, `/Grades=code`, or `/Qualifiers=identifier` filters to filter individual points to export.

- By default, all points of the time-series will be exported.
- More than one point filter can be specified on the command line or in a configuration file.
- Each filter operates as either an **inclusion** or **exclusion** filter.
- A filter is assumed to be an **inclusion** filter, unless the value begins with a minus-sign.
- Approvals and grades can also begin with comparison operators (`<=`, `<`, `>` and `>=`) to perform numeric comparisons and thresholding.
- Qualifiers can only be matched by equality.

Eg. Only export points with an "In Review" or greater approval and a "Monthly" qualifier, but excluding any grades below "Poor"

```cmd
SosExporter /Approvals=">=In Review" /Qualifiers=Monthly /Grades="<Poor" (other options ...)
```
## Showing the `/help` screen

The `/help` or `-h` options will show the basic usage help screen.

```cmd
C:\> SosExporter -help

Export time-series changes in AQTS time-series to an OGC SOS server.

usage: SosExporter [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  ============================ Export configuration settings. Changes will trigger a full resync:
  -AquariusServer              AQTS server name
  -AquariusUsername            AQTS username [default: admin]
  -AquariusPassword            AQTS password [default: admin]
  -SosServer                   SOS server name
  -SosUsername                 SOS username
  -SosPassword                 SOS password

  ============================ /Publish/v2/GetTimeSeriesUniqueIdList settings. Changes will trigger a full resync:
  -LocationIdentifier          Optional location filter.
  -Publish                     Optional publish filter. [default: True]
  -ChangeEventType             Optional change event type filter. One of Data, Attribute
  -Parameter                   Optional parameter filter.
  -ComputationIdentifier       Optional computation filter.
  -ComputationPeriodIdentifier Optional computation period filter.
  -ExtendedFilters             Extended attribute filter in Name=Value format. Can be set multiple times.

  ============================ Aggressive time-series filtering. Changes will trigger a full resync:
  -TimeSeries                  Time-series identifier regular expression filter. Can be specified multiple times.
  -TimeSeriesDescription       Time-series description regular expression filter. Can be specified multiple times.
  -Approvals                   Filter points by approval level or name. Can be specified multiple times.
  -Grades                      Filter points by grade code or name. Can be specified multiple times.
  -Qualifiers                  Filter points by qualifier. Can be specified multiple times.

  ============================ Maximum time range of points to upload: Changes will trigger a full resync:
  -MaximumPointDays            Days since the last point to upload, in Frequency=Value format. [default:
    Unknown  = 90
    Annual   = All
    Monthly  = All
    Weekly   = 3653
    Daily    = 3653
    Hourly   = 365
    Points   = 30
    Minutes  = 30
  ]

  ============================ Other options: (Changing these values won't trigger a full resync)
  -ConfigurationName           The name of the export configuration, to be saved in the AQTS global settings. [default: SosConfig]
  -DryRun                      When true, don't export to SOS. Only log the changes that would have been performed. [default: False]
  -ForceResync                 When true, force a full resync of all time-series. [default: False]
  -NeverResync                 When true, avoid full time-series resync, even when the algorithm recommends it. [default: False]
  -ChangesSince                The starting changes-since time in ISO 8601 format. Defaults to the saved AQTS global setting value.
  -MaximumPointsPerObservation The maximum number of points per SOS observation [default: 1000]
  -MaximumExportDuration       The maximum duration before polling AQTS for more changes, in hh:mm:ss format.  Defaults to the AQTS global setting.
  -Timeout                     The timeout used for all web requests, in hh:mm:ss format. [default: 5 minutes]

ISO 8601 timestamps use a yyyy'-'mm'-'dd'T'HH':'mm':'ss'.'fffffffzzz format.

  The 7 fractional seconds digits are optional.
  The zzz timezone can be 'Z' for UTC, or +HH:MM, or -HH:MM

  Eg: 2017-04-01T00:00:00Z represents April 1st, 2017 in UTC.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace are ignored.
  Comment lines begin with a # or // marker.
```