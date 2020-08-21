# NEM12PreProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `NEM12PreProcessor.exe` is a console utility to extract time-series data from power meters in a [NEM12 Meter Data CSV File](https://www.aemo.com.au/-/media/files/electricity/nem/retail_and_metering/metering-procedures/2018/mdff-specification-nem12--nem13-v106.pdf?la=en).

The sensor data can be processed by [AQUARIUS EnviroSCADA](https://aquaticinformatics.com/products/aquarius-enviroscada/), AQUARIUS DAS, or AQUARIUS Connect and appended to a time-series in [AQUARIUS Time-Series](https://aquaticinformatics.com/products/aquarius-time-series/).

`NEM12PreProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.


## Output text stream format

Each time the tool runs, it queries the WatchWatch API for any new measurements.

Each new measurement will be output to standard out as the following CSV stream:

```csv
Iso8601UtcTime, SensorType, SensorSerial, Value
2018-12-31T14:35:00.000Z, LS1, 418892, 0.09223456
2018-12-31T14:37:00.000Z, LS1, 40AD1C, 0.28947586
```

## Integrating with AQUARIUS Connect, or AQUARIUS EnviroSCADA, or AQUARIUS DAS

The integration for the family of AQUARIUS data ingest products share some common configuration.
