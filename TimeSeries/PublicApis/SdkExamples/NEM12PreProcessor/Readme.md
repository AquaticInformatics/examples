# NEM12PreProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `NEM12PreProcessor.exe` is a console utility to extract time-series data from power meters in a [NEM12 Meter Data CSV File](https://www.aemo.com.au/-/media/files/electricity/nem/retail_and_metering/metering-procedures/2018/mdff-specification-nem12--nem13-v106.pdf?la=en).

The sensor data can be processed by [AQUARIUS EnviroSCADA](https://aquaticinformatics.com/products/aquarius-enviroscada/), AQUARIUS DAS, or AQUARIUS Connect and appended to a time-series in [AQUARIUS Time-Series](https://aquaticinformatics.com/products/aquarius-time-series/).

`NEM12PreProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.

## Output text stream format

```csv
NationalMeteringIdentifier, RegisterId, UnitOfMeasure, Time, Value, QualityMethod, ReasonCode, ReasonDescription
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

## Operation modes

### Reading NEM12 records from the standard input stream

Given no other command line arguments, `NEM12PreProcessor` will read from standard input, and expects to encounter NEM12 input records.

This is the common preprocessor mode expected by AQUARIUS Connect, EnviroSCADA, or DAS integrations. Connect/EnviroSCADA/DAS is responsible for reading the actualy files, and feeding the file contents into a preprocessor for custom processing into a text stream which can then be parsed by the core product.

### Reading files of NEM12 content

You can also suply a filename or path as a command line argument, and the EXE will read that file and decode its contents as NEM12 records. This is useful for debugging the preprocessor to see what it might do with a given data stream.

```sh
NEM12PreProcessor.exe myfile.dat | head

NationalMeteringIdentifier, RegisterId, UnitOfMeasure, Time, Value, QualityMethod, ReasonCode, ReasonDescription
NEEE001397, E3, KWH, 2020-07-06 00:15, 97.984, A, , 
NEEE001397, E3, KWH, 2020-07-06 00:30, 113.04, A, , 
NEEE001397, E3, KWH, 2020-07-06 00:45, 110.576, A, , 
NEEE001397, E3, KWH, 2020-07-06 01:00, 108.24, A, , 
NEEE001397, E3, KWH, 2020-07-06 01:15, 106.704, A, , 
NEEE001397, E3, KWH, 2020-07-06 01:30, 106.448, A, , 

NEM12PreProcessor.exe myfile.dat | tail

NEEE001397, K1, KVARH, 2020-07-06 22:45, 0, A, , 
NEEE001397, K1, KVARH, 2020-07-06 23:00, 0, A, , 
NEEE001397, K1, KVARH, 2020-07-06 23:15, 0, A, , 
NEEE001397, K1, KVARH, 2020-07-06 23:30, 0, A, , 
NEEE001397, K1, KVARH, 2020-07-06 23:45, 0, A, , 
NEEE001397, K1, KVARH, 2020-07-07 00:00, 0, A, , 
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
