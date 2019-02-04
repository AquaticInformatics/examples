# WaterWatchPreProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `WaterWatchPreProcessor.exe` is a console utility to extract time-series data from sensors in a [WaterWatch.io](https://www.waterwatch.io/) organisation account.

The sensor data can be processed by [AQUARIUS EnviroSCADA](https://aquaticinformatics.com/products/aquarius-enviroscada/), AQUARIUS Connect, or AQUARIUS DAS and appended to a time-series in [AQUARIUS Time-Series](https://aquaticinformatics.com/products/aquarius-time-series/).

`WaterWatchPreProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.

## Configuration options

The tool supports the [common command line options](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) pattern used by many AQTS utilities, which gives you some flexibility for configuring your integration.

At a minimum, this tool needs three pieces of information from your WaterWatch account, which can be found in the [Settings/APIs & Integration](https://help.waterwatch.io/article/20-waterwatch-rest-api-documentation) menu of the WaterWatch web application:
- `/WaterWatchOrgId=<orgId>` - Organisation ID
- `/WaterWatchApiKey=<apiKey>` - API Key
- `/WaterWatchApiKey=<apiToken>` - API Token

## Output text stream format

Each time the tool runs, it queries the WatchWatch API for any new measurements.

Each new measurement will be output to standard out as the following CSV stream:

```csv
Iso8601UtcTime, SensorType, SensorSerial, Value
2018-12-31T14:35:00.000Z, LS1, 418892, 92.23456
2018-12-31T15:35:00.000Z, LS1, 418892, 89.47586
```

## `/help` screen

```
Extract the latest sensor readings from a https://waterwatch.io account

usage: WaterWatchPreProcessor [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  -WaterWaterOrgId      WaterWatch.io organisation Id
  -WaterWaterApiKey     WaterWatch.io API key
  -WaterWaterApiToken   WaterWatch.io API token
  -SaveStatePath        Path to persisted state file [default: WaterWatchSaveState.json]

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace are ignored.
  Comment lines begin with a # or // marker.
```
