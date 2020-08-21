# NEM12PreProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `NEM12PreProcessor.exe` is a console utility to extract time-series data from power meters in a [NEM12 Meter Data CSV File](https://www.aemo.com.au/-/media/files/electricity/nem/retail_and_metering/metering-procedures/2018/mdff-specification-nem12--nem13-v106.pdf?la=en).

The sensor data can be processed by [AQUARIUS EnviroSCADA](https://aquaticinformatics.com/products/aquarius-enviroscada/), AQUARIUS DAS, or AQUARIUS Connect and appended to a time-series in [AQUARIUS Time-Series](https://aquaticinformatics.com/products/aquarius-time-series/).

`NEM12PreProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.

## Output text stream format

The register value for meter interval is output on a separate line.

```csv
NationalMeteringIdentifier, RegisterId, UnitOfMeasure, Time, Value, QualityMethod, ReasonCode, ReasonDescription
6203772322, E1, KWH, 2018-10-04 00:00, 30.256, A, , 
6203772322, E1, KWH, 2018-10-04 00:30, 30.784, A, , 
6203772322, E1, KWH, 2018-10-04 01:00, 30.432, A, , 
6203772322, E1, KWH, 2018-10-04 01:30, 30.864, A, , 
6203772322, E1, KWH, 2018-10-04 02:00, 30.464, A, , 
6203772322, E1, KWH, 2018-10-04 02:30, 30.592, A, , 
6203772322, E1, KWH, 2018-10-04 03:00, 30.832, A, , 
6203772322, E1, KWH, 2018-10-04 03:30, 31.52, A, , 
6203772322, E1, KWH, 2018-10-04 04:00, 31.808, A, , 
6203772322, E1, KWH, 2018-10-04 04:30, 31.808, A, , 
6203772322, E1, KWH, 2018-10-04 05:00, 31.104, A, , 
6203772322, E1, KWH, 2018-10-04 05:30, 29.68, A, , 
6203772322, E1, KWH, 2018-10-04 06:00, 29.376, A, , 
6203772322, E1, KWH, 2018-10-04 06:30, 32.096, A, , 
6203772322, E1, KWH, 2018-10-04 07:00, 39.632, A, , 
6203772322, E1, KWH, 2018-10-04 07:30, 39.232, A, , 
6203772322, E1, KWH, 2018-10-04 08:00, 42.932, F17, 0, Free text description
6203772322, E1, KWH, 2018-10-04 08:30, 55.428, F17, 0, Free text description
6203772322, E1, KWH, 2018-10-04 09:00, 58.9, A, 89, 
```

## Integrating with AQUARIUS Connect, or AQUARIUS EnviroSCADA, or AQUARIUS DAS

All of the AQUARIUS Platform products supporting file processing support a configurable preprocessor option.

See the specific product manual for details.

## `/help` screen

```
Converts the NEM12 CSV file into a single CSV row per point.

usage: NEM12PreProcessor [-option=value] [@optionsFile] NEM12File1 NEM12File2 ...

If no NEM12 CSV file is specified, the standard input will be used.

Supported -option=value settings (/option=value works too):

  -Files                Parse the NEM12 file. Can be set multiple times.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace are ignored.
  Comment lines begin with a # or // marker.
```