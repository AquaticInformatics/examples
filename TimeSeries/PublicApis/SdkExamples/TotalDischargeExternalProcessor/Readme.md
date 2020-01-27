# TotalDischargeExternalProcessor.exe

Download the latest version from [here](https://github.com/AquaticInformatics/examples/releases/latest).

The `TotalDischargeExternalProcessor.exe` console utility can be scheduled to calculate Total Discharge (QV) for the duration of a sampling event.

The resulting total discharge points are appended back to a reflected time-series in your AQTS system, and which is used in calc-derived time-series to calculate total loading values for lab data measured during the sampling event.

The tool can automatically create and configure any missing intermediate time-series to perform the total loading calculations.

## Processing sequence

Each output **DischargeTotalTimeSeries** (ParameterId=**QV**) is derived from:
- Required: **EventTimeSeries**: An event detected from a sampling time-series, (typically an incrementing bottle count from a sampler).
- Required: **DischargeTimeSeries**: A discharge time-series (ParameterId=**QR**)
- Optional: **MinimumEventDuration**: All events will be at least this duration. Defaults to 2 hours

Each calculation derived from the **DischargeTotalTimeSeries** requires two properties:
- Required: **SamplingSeries** should be a time-series with a parameter measured in mass-per-volume units.
- Required: **TotalLoadingSeries** should be a time-series with a parameter measured in mass units.

It is common to have more than one calculation derived from a single **DischargeTotalTimeSeries**, since each location might be sampling many parameters (eg. Chloride and Nitrates).

An event starts whenever the **EventTimeSeries** corrected value begins to increment or resets.
The event ends after the **MinimumEventDuration** elapses with no change in the **EventTimeSeries** corrected value.

The total discharge volume for the duration of the event of is calculated from the correct discharge signal, using the trapezoidal integration method for discharge points which are within the event interval.

Unit conversion for time, volume, and mass is performed as needed.
- It is fine to have "Discharge.Working@Location" in cubic metres per second ("m^3/s") and calculate "Discharge Total.Sampling@Location" in mega-litres ("Ml").
- It is fine to have "Chloride.LabData@Location" in milligrams per litre ("mg/l") and calculate "Total Chloride.Event@Location" in kilograms ("kg").

## Time-series requirements

- The **DischargeTimeSeries** must be of ParameterId=**QR**. Any valid **Volumetric Flow** unit is permitted. The time-series can be basic, reflected, or derived.
- The **DischargeTotalTimeSeries** must be of ParameterId=**QV**. Any valid **Volume** unit is permitted. This must be a reflected time-series, with no gap interval defined.
- There are no restrictions on the **EventTimeSeries**, other than having a value which increments by any amount during the **MinimumEventDuration**.
- Each **SamplingSeries** must be measuring a parameter with a mass-per-volume unit.  Many of the units in the built-in `Concentration` unit group meet this requirement. (eg. `mg/l` or `g/m^3`).
- Each **TotalLoadingSeries** must be measured in a **Mass** unit.

One of the best sources of a **SamplingSeries** is an [AQUARIUS Samples](https://aquaticinformatics.com/products/aquarius/aquarius-samples/) observation synchronized into AQUARIUS Time Series using the SamplesConnector service, but you are free to use any time-series that measures a parameter in mass-per-volume units.

## Automatically create missing time-series

The `/CreateMissingTimeSeries=` option defaults to `false`, but can be set to `true` to automatically create any missing **DischargeTotalTimeSeries** or **TotalLoadingSeries** as needed.

- **DischargeTotalTimeSeries** will be created as a **Reflected** time-series, using the **DefaultDischargeTotalUnit** if set, otherwise use the "QV" parameter's default unit.
- **TotalLoadingTimeSeries** will be created as a **Calc-derived** time-series, using the **DefaultTotalLoadingUnit** if set, otherwise use the total loading parameter's default unit.

The unit used to create each series follow three rules:
- Use the specific unit property (**DischargeTotalUnit** or **TotalLoadingUnit**) if specified.
- Otherwise, use the default unit property (**DefaultDischargeTotalUnit** or **DefaultTotalLoadingUnit**) if specified.
- Otherwise, use the parameter's default unit.

Once your configuration is stable, it is recommended to keep this setting at `false` to avoid unexpected time-series creation in a production environment.

## Installation instructions

- `TotalDischargeExternalProcessor.exe` is a stand-alone EXE. It requires no installer and can be run from any folder.
- The utility runs on any up-to-date Windows system.
- The `TotalDischargeExternalProcessor.log` file will contain a history of all activity performed.

## Configuration options

The tool supports the [common command line options](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options) pattern used by many AQTS utilities, which gives you some flexibility for configuring your integration.

Options are case insenstive, and can be begin with a dash (-) or a slash (/), so they work equally well from CMD.EXE, PowerShell, or a bash terminal window.

This is an example 7-line `Options.txt` file:
```
# Set your AQTS credentials here
/server=myappserver
/username=myuser
/password=password123

# Use a configuration file from a non-standard location
/configPath=C:\My Scripts\MySpecialLoadingConfiguration.json
```

## `Config.json` stores the loading configuration

This tool uses a JSON file to define the configuration of all the loading calculations performed.

The tool will look for a file named `Config.json` in the same folder as the EXE, unless you use the `/ConfigPath=` option to point to a configuration file somewhere else.

Editing JSON files can be tricky, so please read the [JSON editing tips](#json-editing-tips) section at the end of this page.

### Follow some standard conventions, type less JSON text, and save your wrists!

This tool works best when each location follows the expected naming conventions for the time-series used to calculate total loading.

All of the naming conventions are configurable, and each convention can be overridden for a specific location or sampled parameter, since the real world always has weird edge-cases.

But the more each location conforms to the expected naming conventions, the less text you will need to type into the `Config.json` file.

When your location or configuration isn't quite standard enough, you only need to override the non-standard bits.

When every location follows the conventions, you only need to specify each location, and each sampled parameter. Everything else (all the involved time-series labels and units) can be inferred.

This design approach is called being "wrist friendly".

### Best convention for smaller `Config.json` files

There are 8 properties which can be configured in the `Defaults` object at the start of the `Config.json` file, to defined the expected naming conventions.

|Property name|Default value|Description|
|---|---|---|
| **EventParameterAndLabel** | `Count.Bottle` | This is the parameter and label assumed to exist at each location calculating total load. |
| **EventLabel** | `Event` | This label will be assumed for both the total discharge series and each total loading series.|
| **TotalLoadingPrefix** | `Total ` | This prefix is applied to every sampled parameter. Essentially, if you are measuring "Chloride", the convention expects that you will calculate the total loading in a parameter named "Total Chloride". <br/><br/>**Note**: There is a deliberate space after "Total" |
| **SamplingLabel** | `LabData` | This label is assumed for every sampling series used to create each total loading series. |
| **DischargeLabel** | `Working` | This label is asummed to select the best discharge series at the location for calculating total discharge. |
| **DischargeTotalUnit** | (no default) | If a total discharge time-series needs to be created, create the time-series with this unit. When no unit is specified, use the "QV" parameter's default unit. |
| **TotalLoadingUnit** | (no default) | If a total loading time-series needs to be created, create the time-series with this unit. When no unit is specified, use the total loading parameter's default unit.  |
| **MinimumEventDuration** | `2:00` | This 2-hour duration is a [.NET TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan.parse?view=netframework-4.8#System_TimeSpan_Parse_System_String_) value, which expects a "hh:mm" format. |

### How to specify time-series identifiers in JSON

The tool uses the default conventions and the location identifier to infer as much about the time-series identifiers as possible.

In any of the places the JSON expects a time-series identifier, the following rules apply.

- You can specify a `UniqueId` value (eg. `c9ccaabaf871476ba7e383c5b68e59b8`)
- You can specify a full time-series identifier, in "Parameter.Label@Location" format. (eg. `Discharge.Working@08HK1234`)
- If your time-series identifier omits the `@location` component, the current processor's **Location** property will be used.
- If your time-series identifier omits the `.Label` component, the appropriate default label will be used.

The result is that for most common cases, you only need to specify the parameter type for a **SamplingSeries**, and everything else can be inferred from the location and defaults.

But you are always able to specify a different time-series if that makes more sense.

### Convention-based vs. fully-specified JSON

This section will present the same JSON configuration, for a 2-location system, following the default naming conventions.

- LocA samples Chloride
- LocB samples Chloride and Nitrate

#### The smallest-possible JSON

This JSON document just specifies the locations, and the parameters sampled at each location.

Everything else is inferred from conventions.

```json
{
  "Processors": [
    {
      "Location": "LocA",
      "Calculations": [
        {"SamplingSeries": "Cl (Dis)"}
      ]
    },
    {
      "Location": "LocB",
      "Calculations": [
        {"SamplingSeries": "Cl (Dis)"},
        {"SamplingSeries": "NO3 (Dis)"}
      ]
    }
  ]
}
```

#### The largest-possible JSON

This is the identical configuration, but with everything explictly named.

That's why following naming conventions is a better approach.

```json
{
  "Defaults": {
    "EventParameterAndLabel": "Count.Bottle",
    "EventLabel": "Event",
    "TotalLoadingPrefix": "Total ",
    "SamplingLabel": "LabData",
    "DischargeLabel": "Discharge",
    "DischargeTotalUnit": "Mm^3",
    "TotalLoadingUnit": "kg",
    "MinimumEventDuration": "02:00"
  },
  "Processors": [
    {
      "Location": "LocA",
      "EventTimeSeries": "Count.Bottle@LocA",
      "DischargeTimeSeries": "Discharge.Working@LocA",
      "DischargeTotalTimeSeries": "Discharge Total.Event@LocA",
      "DischargeTotalUnit": "Mm^3",
      "MinimumEventDuration": "02:00",
      "Calculations": [
        {
          "SamplingSeries": "Cl (Dis).LabData@LocA",
          "TotalLoadingSeries": "Total Cl (Dis).Event@LocA",
          "TotalLoadingUnit": "kg"
        }
      ]
    },
    {
      "Location": "LocB",
      "EventTimeSeries": "Count.Bottle@LocB",
      "DischargeTimeSeries": "Discharge.Working@LocB",
      "DischargeTotalTimeSeries": "Discharge Total.Event@LocB",
      "DischargeTotalUnit": "Mm^3",
      "MinimumEventDuration": "02:00",
      "Calculations": [
        {
          "SamplingSeries": "Cl (Dis).LabData@LocB",
          "TotalLoadingSeries": "Total Cl (Dis).Event@LocB",
          "TotalLoadingUnit": "kg"
        },
        {
          "SamplingSeries": "NO3 (Dis).LabData@LocB",
          "TotalLoadingSeries": "Total NO3 (Dis).Event@LocB",
          "TotalLoadingUnit": "kg"
        }
      ]
    }
  ]
}
```

## Scheduling the external processor.

When no other command line options are given, the tool looks for the `Options.txt` file in the same folder as the EXE. If the `Options.txt` file exists, its contents are used.

This makes scheduling the tool from the Windows Task Scheduler a very thing. Just store an `Options.txt` file next the the EXE and simply schedule the EXE to run at the desired frequency.

## JSON editing tips

Editing [JSON](https://json.org) can be a tricky thing.

Sometimes the code can detect a poorly formatted JSON document and report a decent error, but sometimes a poorly formatted JSON document will appear to the code as just an empty document.

These "silent failures" can be frustrating to debug.

Here are some tips to help eliminate common JSON config errors:
- Edit JSON in a real text editor. Notepad is fine, [Notepad++](https://notepad-plus-plus.org/) or [Visual Studio Code](https://code.visualstudio.com/) are even better choices.
- Don't try editing JSON in Microsoft Word. Word will mess up your quotes and you'll just have a bad time.
- Try validating your JSON using the online [JSONLint validator](https://jsonlint.com/).
- Whitespace between items is ignored. Your JSON document can be single (but very long!) line, but the convention is separate items on different lines, to make the text file more readable.
- All property names must be enclosed in double-quotes (`"`). Don't use single quotes (`'`) or smart quotes (`“` or `”`), which are actually not that smart for JSON!
- Avoid a trailing comma in lists. JSON is very fussy about using commas **between** list items, but rejects lists when a trailing comma is included. Only use a comma to separate items in the middle of a list.

### Adding comments to JSON

The JSON spec doesn't support comments, which is unfortunate.

However, the code will simply skip over properties it doesn't care about, so a common trick is to add a dummy property name/value string. The code won't care or complain, and you get to keep some notes close to other special values in your custom JSON document.

Instead of this:

```json
{
  "ExpectedPropertyName": "a value",
  "AnotherExpectedProperty": 12.5 
}
```

Try this:

```json
{
  "_comment_": "Don't enter a value below 12, otherwise things break",
  "ExpectedPropertyName": "a value",
  "AnotherExpectedProperty": 12.5 
}
```

Now your JSON has a comment to help you remember why you chose the `12.5` value.

## Help screen `/help` or `-help`

```
C:\Some\Folder> TotalDischargeExternalProcessor.exe -help                                                                          14:19:18.555 ERROR - An external processor for calculating total discharge for arbitrary-length events.

usage: TotalDischargeExternalProcessor [-option=value] [@optionsFile] processor ...

Supported -option=value settings (/option=value works too):

  -Server                   AQTS server name
  -Username                 AQTS username [default: admin]
  -Password                 AQTS password [default: admin]
  -ConfigPath               Path to the JSON configuration file. [default: 'Config.json' in the same folder as the EXE]
  -CreateMissingTimeSeries  When true, any missing time-series will be created. [default: True]

When no other command line options are given, the Options.txt file in
same folder as the EXE will be used if it exists.

Use the @optionsFile syntax to read more options from a file.

  Each line in the file is treated as a command line option.
  Blank lines and leading/trailing whitespace is ignored.
  Comment lines begin with a # or // marker.
```