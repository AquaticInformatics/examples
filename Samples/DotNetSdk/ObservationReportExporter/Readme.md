# ObservationReportExporter

Download the [latest ObservationReportExporter.exe release here](../../../../../releases/latest)

The `ObservationReportExporter` tool is a standalone .NET console utility for exporting AQUARIUS Samples observations using a spreadsheet template and attaching the generated spreadsheet to the equivalent AQUARIUS Time-Series location.

The intent is to run this tool on a schedule so that the uploaded location attachments can be made accessible via AQUARIUS Web Portal.

## Features

- With no filters specified, all observed values will be exported.
- Results can be filtered by optional date range, location, project, property, or analytical group.
- All filters are cumulative (ie. they are AND-ed together). The more filters you add, the fewer results will be exported.
- An exit code of 0 indicates the export was successful. An exit code greater than 0 indicates an error. This allows for easy error checking when invoked from scripts.

## Requirements

- The .NET 4.7 runtime is required, which is pre-installed on all Windows 10 and Windows Server 2016 systems, and on nearly all up-to-date Windows 7 and Windows Server 2008 systems.
- No installer is needed. It is just a single .EXE which can be run from any folder.

### Command line option syntax

All command line options are case-insensitive, and support both common shell syntaxes: either `/Name=value` (for CMD.EXE) or `-Name=value` (for bash and PowerShell).

In addition, the [`@options.txt` syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) is supported, to read options from a text file. You can mix and match individual `/name=value` and `@somefile.txt` on the same command line.

Try the `/help` option for a detailed list of options and their default values.

## Authentication with AQUARIUS Samples

Two options are required to tell the tool how to access your AQUARIUS Samples instance.

- The `/SamplesServer=` option sets the URL to the server, usually something like `/SamplesServer=https://mydomain.aqsamples.com`.
- The `/SamplesApiToken=` option sets the API token used to authenticate your account. Navigate to https://[your_account].aqsamples.com/api/ and follow the instructions to get the token.


## Authentication with AQUARIUS Time-Series

Three options are required to tell the tool how to access your AQUARIUS Time-Series system.

- The `/TimeSeriesServer=` option sets the name of the AQTS server, usually something like `/TimeSeriesServer=https://myappserver`.
- The `/TimeSeriesUsername=` and `TimeSeriesPassword` options set the AQTS credentials to use for uploading the generated reports.

### `/help` screen

```
Export observations from AQUARIUS Samples using a spreadsheet template and into AQUARIUS Time-Series.

usage: ObservationReportExporter [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  =========================== AQUARIUS Samples connection options:
  -SamplesServer              AQS server URL
  -SamplesApiToken            AQS API Token

  =========================== AQUARIUS Time-Series connection options:
  -TimeSeriesServer           AQTS server
  -TimeSeriesUsername         AQTS username
  -TimeSeriesPassword         AQTS password

  =========================== Export options:
  -ExportTemplateName         The Observation Export Spreadsheet Template to use for all exports.
  -AttachmentFilename         Name of the generated report. Existing attachments with the same name will be deleted. [default: Report.xlsx]
  -AttachmentTags             Uploaded attachments will have these tag values applies, in key:value format.
  -DeleteExistingAttachments  Delete any existing location attachments with the same name. [default: True]
  -DryRun                     When true, don't export and upload reports, just validate what would be done. [default: False]

  =========================== Cumulative filter options: (ie. AND-ed together). Can be set multiple times.
  -StartTime                  Include observations after this time.
  -EndTime                    Include observations before this time.
  -LocationId                 Observations matching these sampling locations.
  -LocationGroupId            Observations matching these sample location groups.
  -AnalyticalGroupId          Observations matching these analytical groups.
  -ObservedPropertyId         Observations matching these observed properties.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```
