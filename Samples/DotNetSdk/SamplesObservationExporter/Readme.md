# SamplesObservationExporter

Download the [latest SamplesObservationExporter.exe release here](../../../../../../releases/latest)

The `SamplesObservationExporter` tool is a standalone .NET console utility for exporting observed property values from AQUARIUS Samples to a CSV file.

## Features

- With no filters specified, all observed values will be exported.
- Results can be filtered by optional date range, location, project, property, or analytical group.
- All filters are cumulative (ie. they are AND-ed together). The more filters you add, the fewer results will be exported.

## Requirements

- The .NET 4.7 runtime is required, which is pre-installed on all Windows 10 and Windows Server 2016 systems, and on nearly all up-to-date Windows 7 and Windows Server 2008 systems.
- No installer is needed. It is just a single .EXE which can be run from any folder.

### Command line option syntax

All command line options are case-insensitive, and support both common shell syntaxes: either `/Name=value` (for CMD.EXE) or `-Name=value` (for bash and PowerShell).

In addition, the [`@options.txt` syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) is supported, to read options from a text file. You can mix and match individual `/name=value` and `@somefile.txt` on the same command line.

Try the `/help` option for a detailed list of options and their default values.

## Authentication with AQUARIUS Samples

Two options are required to tell the tool how to access your AQUARIUS Samples instance.

- The `/ServerUrl=` option sets the URL to the server, usually something like `/ServerUrl=https://mydomain.aqsamples.com`.
- The `/ApiToken=` option sets the API token used to authenticate your account. See the online help to determine how to collect your API token.

### Output options

- The `/CsvOutputPath=` option specifies the where to write the CSV file. If omitted, then `ExportedObservations-yyyyMMddHHmmss.csv` will be created in the same folder as the EXE.
- The `/UtcOffset=` option defaults to the systems current timezone if omitted.
- The `/Overwrite=` option can be set to true if you want to allow an existing CSV file to be overwritten.

### Filtering observations

- The `/StartTime` and `/EndTime=` options can be used to constrain the matched visits by observation time.
- The `/LocationIds=`, `/AnalyticalGroupIds=`, `/ObservedPropertyIds=`, and `/ProjectIds=` options can be use to filter by these properties as well.
- If a supplied filter is invalid (eg. you misspelled the location ID), an error message will be displayed an no export will occur.

### Examples

Here we'll just export everthing (this can take a while!):

```
$ SamplesObservationExporter.exe -serverUrl=https://ai.aqsamples.com -apiToken=12345678

17:08:57.436 INFO  - Connecting to https://ai.aqsamples.com ...
17:08:57.945 INFO  - Connected to https://ai.aqsamples.com/api (2020.4.3767) ...
17:08:58.201 INFO  - Exporting observations  ...
17:08:58.973 INFO  - Fetching all 18804 matching observations.
17:10:32.626 INFO  - 18546 numeric observations loaded in 1 minute, 33 seconds.
17:10:32.646 INFO  - Writing observations to 'ExportedObservations-20200827170857.csv' ...
```

Here we will only export temperatures during 2019:

```
$ SamplesObservationExporter.exe -serverUrl=https://ai.aqsamples.com -apiToken=12345678 -StartTime=2019-01-01 -EndTime=2019-12-31T23:59.59 -ObservationPropertyId=Temperature

17:18:10.404 INFO  - Connecting to https://ai.aqsamples.com ...
17:18:10.950 INFO  - Connected to https://ai.aqsamples.com/api (2020.4.3767) ...
17:18:11.658 INFO  - Exporting observations after 2019-01-01T00:00:00.0000000-08:00 matching before 2019-12-31T23:59:59.0000000-08:00 with observed property 'Temperature' ...
17:18:12.164 INFO  - Fetching all 3 matching observations.
17:18:12.182 INFO  - 3 numeric observations loaded in 2 milliseconds.
17:18:12.193 INFO  - Writing observations to 'C:\git\Examples\Samples\DotNetSdk\SamplesObservationExporter\bin\Debug\ExportedObservations-20200827171810.csv' ...```
```

### Output CSV format

- The CSV output is sorted by time, with some common location columns, and with a pair of value and unit columns for each exported property.
- Some columns will be sparesly populated (empty unit and value columns) if that property was not measured at the time.
- Non-detects will have an empty value column, and the unit column will show the minimum detection limit, like `< 3 mg/L`.

### `/help` screen

```
Export observations from an AQUARIUS Samples server.

usage: SamplesObservationExporter [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  ========================= AQSamples connection options:
  -ServerUrl                AQS server URL
  -ApiToken                 AQS API Token

  ========================= Output options:
  -CsvOutputPath            Path to output file [default: ExportedObservations-yyyyMMddHHmmss.csv in the same folder as the EXE]
  -Overwrite                Overwrite existing files? [default: False]
  -UtcOffset                UTC offset for output times [default: Use system timezone, currently -07:00]

  ========================= Cumulative filter options: (ie. AND-ed together)
  -StartTime                Include observations after this time.
  -EndTime                  Include observations before this time.
  -LocationIds              Observations matching these locations.
  -AnalyticalGroupIds       Observations matching these analytical groups.
  -ObservedPropertyIds      Observations matching these observed properties.
  -ProjectIds               Observations matching these projects.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```