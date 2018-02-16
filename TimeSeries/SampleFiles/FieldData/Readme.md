# Supported field data file types

| Type | File extensions | Description | Imported activity types |
| --- | --- | --- | --- |
| [Stage Discharge](Plugin/StageDischarge) (plugin) | `*.*`<br/>(typically `*.csv`) | Reads stage/discharge measurements from CSV. | Discharge summary. |
| [Pocket Gauger](Plugin/PocketGauger) (plugin) | `*.*`<br/>(typically `*.zip`) | Reads gaugings from a Pocket Gauger device.<br/>Commonly used in the United Kingdom. | Point velocity discharge.<br/>Current meter calibrations. |
| [Cross Section Survey](Plugin/CrossSectionSurvey) (plugin) | `*.*`<br/>(typically `*.csv`) | Reads cross-section surveys from CSV. | Cross section survey. |
| [AquaCalc Pro](Stock/AquaCalc) | `*.csv` | Reads discharge measurements from an AquaCalc stream flow computer. | Point velocity discharge.|
| [WinRiverII HydroML](Stock/WinRiver) | `*.xml` | Reads ADCP measurements from Teledyne RDI's WinRiver II software.  | ADCP discharge.<br/>Water temperature readings. |
| [HFC](Stock/HFC) | `*.mq*`, `*.dat` | Reads Hydrometric Field Computer files.<br/>Used by Water Survey Canada. | Point velocity discharge.<br/>Air and water temperature readings.<br/>Current meter calibrations. |
| [FlowTracker DIS](Stock/FlowTracker) | `*.dis` | Reads SonTek FlowTracker (first-generation) discharge measurements. | Point velocity discharge.<br/>Water temperature readings. |
| [Scottech STIL Gauging Logger](Stock/Scottech)| `*.glr` | Reads discharge measurements from a STIL logger.<br/>Commonly used in New Zealand. | Point velocity discharge.<br/>Water temperature readings.<br/>Current meter calibrations. |
| [SonTek RiverSurveyor discharge report](Stock/RiverSurveyor)| `*.dis` | Reads discharge reports from SonTek RiverSurveyor software. | ADCP discharge.<br/>Water temperature readings. |
| [USGS HydroML](Stock/USGS) | `*.xml` | Reads USGS HydroML files. | All discharge types (Point velocity, ADCP, volumetric, flume).<br/>Parameter readings and inspections.<br/>Control conditions.<br/>Gauge height at zero flow.<br/>Current meter calibrations.|

## How does AQUARIUS Time Series know which file type is being imported?

Simply looking at the file extension of an uploaded file is not enough to know how to parse that file, since many different file formats share common file extensions. The `*.csv`, `*.xml`, and `*.dis` file extensions are commonly used for many competing file formats.

When you upload a field data file, AQUARIUS tries each supported parser in the order listed in the above table, and stops when a parser successfully reads a file.

- First, all of the currently-installed field data plugins are given a chance to parse the file.
- Next, all of the stock parsers are given a chance to parse the file.
- If none of the plugins or stock parsers can understand the file, an "Unknown file type" error message will be displayed.

## My file type isn't supported! What do I do?

If you can't convert your field data into one of the supported file types, you can:

- Write your own plugin using the [AQUARIUS Field Data Framework](https://github.com/AquaticInformatics/aquarius-field-data-framework)
- Contact our [Support team](http://aquaticinformatics.com/support/) to see if another alternative exists.
