# LabFileImporter

Import lab files to AQUARIUS samples.

### `/help` screen

```
Import lab file results as AQUARIUS Samples observations.

usage: LabFileImporter [-option=value] [@optionsFile] labFile ...

Supported -option=value settings (/option=value works too):

  ========================= AQSamples connection options: (if set, imported lab results will be uploaded)
  -ServerUrl                AQS server URL
  -ApiToken                 AQS API Token

  ========================= File parsing options:
  -File                     Parse the XLXS as lab file results.
  -BulkImportIndicator      Cell A6 with this value indicates a bulk import format [default: unity water internal]
  -FieldResultPrefix        Row 5 methods beginning with this text indicate a FIELD_RESULT [default: client]

  ========================= Import options:
  -DryRun                   Enable a dry-run of the import? /N is a shorthand. [default: False]
  -StopOnFirstError         Stop on first error? [default: False]
  -UtcOffset                UTC offset for imported times [default: Use system timezone, currently -07:00]
  -ResultGrade              Result grade when value is not estimated.
  -EstimatedGrade           Result grade when estimated. [default: Estimated]
  -FieldResultStatus        Field result status. [default: Preliminary]
  -LabResultStatus          Lab result status. [default: Preliminary]
  -DefaultLaboratory        Default laboratory Id for lab results [default: Unity Water]
  -NonDetectCondition       Lab detect condition for non-detect events. [default: Non-Detect]
  -LabSpecimenName          Lab specimen name [default: Properties]

  ========================= Alias options: (these help you map from your external system to AQUARIUS Samples)
  -LocationAlias            Set a location alias in aliasedLocation;SamplesLocationId format
  -ObservedPropertyAlias    Set an observed property alias in aliasedProperty;aliasedUnit;SamplesObservedPropertyId format

  ========================= CSV output options:
  -CsvOutputPath            Path to output file. If not specified, no CSV will be output.
  -Overwrite                Overwrite existing files? [default: False]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```