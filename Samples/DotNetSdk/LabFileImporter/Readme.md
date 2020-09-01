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

  ========================= Import options:
  -UtcOffset                UTC offset for imported times [default: Use system timezone, currently -07:00]
  -LocationAlias            Set a location alias in aliasedLocation;SamplesLocationId format
  -ObservedPropertyAlias    Set an observed property alias in aliasedProperty;aliasedUnit;SamplesObservedPropertyId format
  -File                     Parse the specified CSV or XLXS as lab file results.

  ========================= Output options:
  -CsvOutputPath            Path to output file. If not specified, no CSV will be output.
  -Overwrite                Overwrite existing files? [default: False]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```