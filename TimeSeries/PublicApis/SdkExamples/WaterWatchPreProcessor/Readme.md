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
- `/WaterWatchApiToken=<apiToken>` - API Token

## Forcing a resync from a specific time

Use the `/SyncFromUtc=` option to force a resync of data from a specific time.

When not specified, the tool will sync from the last known time for each sensor.

## Filter sensors by regular expression

You can specify [.NET regular expressions](http://regexstorm.net/tester?p=WW&i=WWSensor1%0aSensor2%0aWWSensor3) to filter the list of exported sensors by patterns in the sensor name or sensor serial text properties.

Use the `/SensorName=regex` option to filter by the sensor name.

Use the `/SensorSerial=regex` option to filter by the sensor serial number.

The `regex` regular expression just needs to match a portion of the text property being filtered. So `/SensorName=Public` will match all sensors with a name containing the phrase "Public".

- Regular expressions perform case-sensitve matching.
- More than one `/SensorName=regex` or `/SensorSerial=regex` filter option can be specified on the command line or in a configuration file.
- Each filter operates as either an **inclusion** or **exclusion** filter.
- A filter is assumed to be an **inclusion** filter, unless the `regex` value begins with a minus-sign.
- If no **inclusion** filters are specified, all sensors will be included by default.

This pattern of inclusion and exclusion filtering can be used to precisely control which sensors are exported.

Eg. Export all the sensors starting with "Public", except for serial number "ABC123":

```cmd
WaterWatchPreProcessor /SerialName=^Public /SensorSerial="-^ABC123$" (other options ...)
```

## `/OutputMode=OffsetCorrected` is enabled by default

The value reported for each sensor will be the [offset-corrected value](https://help.waterwatch.io/article/23-what-is-the-offset-and-how-do-i-set-it-up), if the sensor has been configured with an offset.

`OffsetCorrectedValue = Offset - RawDistance;`

Use `/OutputMode=RawDistance` to always show the sensor's rawDistance value.

## Output text stream format

Each time the tool runs, it queries the WatchWatch API for any new measurements.

`Iso8601UtcTime, SensorType, SensorSerial, Value`

Each new measurement will be output to standard out as the following CSV stream:


```csv
2018-12-31T14:35:00.000Z, LS1, 418892, 92.23456
2018-12-31T15:35:00.000Z, LS1, 418892, 89.47586
```

## `/help` screen

```
Extract the latest sensor readings from a https://waterwatch.io account

usage: WaterWatchPreProcessor [-option=value] [@optionsFile] ...

Supported -option=value settings (/option=value works too):

  ===================== https://waterwatch.io credentials
  -WaterWatchOrgId      WaterWatch.io organisation Id
  -WaterWatchApiKey     WaterWatch.io API key
  -WaterWatchApiToken   WaterWatch.io API token

  ===================== Configuration options
  -OutputMode           Measurement value output mode. One of OffsetCorrected, RawDistance. [default: OffsetCorrected]
  -SaveStatePath        Path to persisted state file [default: WaterWatchSaveState.json]
  -SyncFromUtc          Optional UTC sync time. [default: last known sensor time]
  -NewSensorSyncDays    Number of days to sync data when a new sensor is detected. [default: 5]

  ===================== Sensor filtering options
  -SensorName           Sensor name regular expression filter. Can be specified multiple times.
  -SensorSerial         Sensor serial number regular expression filter. Can be specified multiple times.

Supported /SyncFromUtc date formats:

  yyyy-MM-dd
  yyyy-MM-ddTHH:mm
  yyyy-MM-ddTHH:mm:ss
  yyyy-MM-ddTHH:mm:ss.fff

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace are ignored.
  Comment lines begin with a # or // marker.
```
