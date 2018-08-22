# SOS Exporter

`SosExporter.exe` is a .NET console utility which can monitor an AQTS 201x system for changes to time-series. When a time-series is changed, then changes will be exported to an OGC SOS server for consumption by LAWA.

- `SosExporter.exe` is a single EXE, with no external dependencies other than the .NET 4.7 runtime. It can be easily deployed from any Windows system.
- Reasonable defaults are assumed. By default, all AQTS time-series with Publish=true will be exported.
- All options can be configured from the command line or from configuration files.
- The exporter works in "incremental" mode when possible, exporting only newly appended points as they appear.
- When an incremental export is not possible, the entire time-series record is exported.
- Changes to the export configuration which break incremental exports will automatically be detected and a full resync will be performed. 
- The `SosExporter.log` file will contain the output of the most recent export cycle.

Try the `/help` option for more details.

## Requirements

- .NET 4.7 runtime (any up-to-date Windows system will contain this runtime).
- Credentials for an AQTS app server and a SOS OGC server.

## Installation

No installation is required. `SosExporter.exe` is a single executable which can be run from any folder.

## Configuration

The exporter is highly configurable. Any changes which affect the time-series being exported will force a full resync.

## Run the exporter on a schedule

The SOS exporter utility will perform one export cycle and then exit. It can be run on a schedule from any standard scheduler like the Windows Task Scheduler.

## Dry-run mode

Adding the `DryRun=true` option will cause the exporter to perform a dry run, logging only the SOS export operations which would have been performed, but not making any changes.
This is useful for debugging configuration changes before deploying them to a production environment.

## Supports advanced filtering of exported time-series and values

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

Use the `/TimeSeries=regex` option to specify .NET regular expressions to filter the time-series identifiers of changed time-series.

- More than one `/TimeSeries=regex` filter option can be specified on the command line or in a configuration file.
- Each `/TimeSeries=regex` filter operates as either an **inclusion** or **exclusion** filter.
- A filter is assumed to be an **inclusion** filter, unless the `regex` value begins with a minus-sign.
- If no **inclusion** filters are specified, all time-series will be included by default.

This pattern of inclusion and exclusion filtering can be used to precisely control which time-series are exported.

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

## Basic `/help` screen

