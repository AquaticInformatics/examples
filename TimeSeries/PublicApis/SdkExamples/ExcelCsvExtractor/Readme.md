## ExcelCsvExtractor

Download the [latest ExcelCsvExtractor.exe release here](../../../../../../releases/latest)

This console utility can extract CSV files from each sheet in an Excel workbook.

Like many of the AQTS console utilities, this tool:
- Requires the .NET 4.7 runtime, which is pre-installed on all up-to-date Windows 7+ systems.
- Is a single EXE, and can be run from any directory.
- It will create a `ExcelCsvExtractor.log` log file in the same folder as the EXE.
- It supports the flexible [`@options` syntax](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) for when the command line gets a bit daunting.
- See the `/help` option for more details.

### Extract only the sheets you need

By default, all the sheets in the workbook will be extracted into separate CSV files, in the same folder as the Excel workbook.

If you just need to extract specific sheets, you can set the `/Sheet=name` or `/Sheet=1-based-index` option multiple times to select specific sheets.

### Use the \{SheetName\} pattern in the `/OutputPath` value

The `/OutputPath=` option supports some useful substitution patterns, surrounded by curly brackets:

| \{pattern} | Replaced with |
|---|---|
| `{ExcelPath}` | The full path of the Excel file, without the ".xlsx" extension. |
| `{SheetName}` | The name of the extracted sheet. |

The default `/OutputPath=` option is `{ExcelPath}.{SheetName}.csv`, which extracts each sheet as a CSV in the same folder as the source Excel workbook.

## Examples

In its simplest form, we just extract all the sheets.

- The only command line argument is the path to the Excel workbook. (the `-ExcelPath=` option prefix is optional.)
- All 4 sheets are extracted into the same folder.

```
ExcelCsvExtractor.exe "C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx"

04-06 09:16:27.046 INFO  - Loading 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx' ...
04-06 09:16:27.245 INFO  - 4 sheets loaded: survey, choices, settings, types
04-06 09:16:27.247 INFO  - Saving 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.survey.csv' ...
04-06 09:16:27.253 INFO  - Saving 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.choices.csv' ...
04-06 09:16:27.255 INFO  - Saving 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.settings.csv' ...
04-06 09:16:27.257 INFO  - Saving 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.types.csv' ...
```

If we only care about the sheet named `survey`, we can just extract that one sheet.

```
ExcelCsvExtractor.exe "C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx" -Sheets=Survey

04-06 09:43:37.872 INFO  - Loading 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx' ...
04-06 09:43:38.052 INFO  - 4 sheets loaded: survey, choices, settings, types
04-06 09:43:38.054 INFO  - Saving 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.survey.csv' ...
```

And here we extract all the sheets, but to a different folder:

```
ExcelCsvExtractor.exe "C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx" -OutputPath=C:\Temp\{SheetName}.csv

04-06 09:54:24.143 INFO  - Loading 'C:\Users\Doug.Schmidt\Downloads\FieldDataCollection 20200803.xlsx' ...
04-06 09:54:24.322 INFO  - 4 sheets loaded: survey, choices, settings, types
04-06 09:54:24.324 INFO  - Saving 'C:\Temp\survey.csv' ...
04-06 09:54:24.329 INFO  - Saving 'C:\Temp\choices.csv' ...
04-06 09:54:24.331 INFO  - Saving 'C:\Temp\settings.csv' ...
04-06 09:54:24.347 INFO  - Saving 'C:\Temp\types.csv' ...
```

## `/help` screen

```
Purpose: Extracts all the sheets in an Excel workbook into multiple CSV files

Usage: ExcelCsvExtractor [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  -ExcelPath         Specifies the Excel workbook to split
  -Sheets            Split out the named sheets. [default: All sheets]
  -OutputPath        Output path of CSVs (default: {ExcelPath}.{SheetName}.csv
  -ColumnSeparator   Separator between columns [default: ,]
  -Overwrite         Set to true to overwrite existing files. [default: False]
  -TrimEmptyColumns  Set to false to retain empty columns at the end of each row. [default: True]
  -DateTimeFormat    Sets the format of any timestamps, using .NET datetime format [default: ISO8601]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```
