# TotalDischargeExternalProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `TotalDischargeExternalProcessor.exe` console utility can be scheduled to calculate Total Discharge (QV) for the duration of a sampling event. The resulting points are appended back to a reflected time-series in your AQTS system.

## Processing sequence

Each output **DischargeTotalTimeSeries** (ParameterId=**QV**) is derived from:
- Required: **EventTimeSeries**: An event detected from a sampling time-series, (typically an incrementing bottle count from a sampler).
- Required: **DischargeTimeSeries**: A discharge time-series (ParameterId=**QR**)
- Optional: **MinimumEventDuration**: All events will be at least this duration. Defaults to 2 hours

An event starts whenever the **EventTimeSeries** corrected value begins to increment or resets.
The event ends after the **MinimumEventDuration** elapses with no change in the **EventTimeSeries** corrected value.

The total discharge volume for the duration of the event of is calculated from the correct discharge signal, using the trapezoidal integration method for discharge points which are within the event interval.

Unit conversion for time and volume is performed as needed. It is fine to use "Discharge.Working@Location" in cubic metres per second ("m^3/s") and calculate "Discharge Total.Sampling@Location" in mega-litres ("Ml").

## Time-series requirements

- The **DischargeTimeSeries** must be of ParameterId=**QR**. Any valid **Volumetric Flow** unit is permitted. The time-series can be basic, reflected, or derived.
- The **DischargeTotalTimeSeries** must be of ParameterId=**QR**. Any valid **Volume** unit is permitted. This must be a reflected time-series, with no gap interval defined.
- There are no restrictions on the **EventTimeSeries**, other than having a value which increments by any amount during the **MinimumEventDuration**.

## Installation instructions

- `TotalDischargeExternalProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.
- The utility runs on any up-to-date Windows system.
- The `TotalDischargeExternalProcessor.log` file will contain a history of all activity performed.

## Configuration options

The tool supports the [common command line options](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) pattern used by many AQTS utilities, which gives you some flexibility for configuring your integration.

Options are case insenstive, and can be begin with a dash (-) or a slash (/), so they work equally well from CMD.EXE, PowerShell, or a bash terminal window.

This is an example 14-line `Options.txt` file:
```
# Set your AQTS credentials here
/server=myappserver
/username=myuser
/password=password123

# Add as many processor configurations as you need, one per line.
# Whitespace between commas is ignored.
#
# EventTimeSeries, DischargeTimeSeries, DischargeTotalTimeSeries

Count.Bottle Number@AQS Duffins Test, Discharge.Working@AQS Duffins Test, Discharge Total.Event@AQS Duffins Test

# This sampler configuration needs a 3 hour duration
Count.Sampler@MyLocation, Discharge.Working@MyLocation, Discharge Total.Sampling@MyLocation, 3:00
```

## Scheduling the external processor.

When no other command line options are given, the tool looks for the `Options.txt` file in the same folder as the EXE. If the `Options.txt` file exists, its contents are used.

This makes scheduling the tool from the Windows Task Scheduler a very thing. Just store an `Options.txt` file next the the EXE and simply schedule the EXE to run at the desired frequency.

## Help screen `/help` or `-help`

```
C:\Some\Folder> TotalDischargeExternalProcessor.exe -help                                                                          14:19:18.555 ERROR - An external processor for calculating total discharge for arbitrary-length events.

usage: TotalDischargeExternalProcessor [-option=value] [@optionsFile] processor ...

Supported -option=value settings (/option=value works too):

  -Server                   AQTS server name
  -Username                 AQTS username [default: admin]
  -Password                 AQTS password [default: admin]
  -MinimumEventDuration     Minimum event duration [default: 2 hours]
  -Processors               Processor configurations. Can be specified more than once.

Configuring processors:
=======================
Processor configurations are a comma-separated list of 3 or 4 values:

/Processors=EventTimeSeries,DischargeTimeSeries,DischargeTotalTimeSeries[,MinimumEventDuration]

- Either time-series identifier strings or uniqueIds can be used.
- The /Processors= prefix is optional.
- Processor configurations are best set in an @optionsFile, for easier editing.

When no other command line options are given, the Options.txt file in
same folder as the EXE will be used if it exists.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```