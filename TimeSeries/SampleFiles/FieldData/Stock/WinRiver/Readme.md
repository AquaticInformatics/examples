# Sample WinRiverII HydroML field data file

AQUARIUS Time-Series can import field data from WinRiver II HydroML files.

## AQUARIUS location identifiers

The `<SiteIdentifier type="SiteNumber">` XML node should contain the AQUARIUS location identifier in order to support automatic upload from Springboard.

If the file does not contain an AQUARIUS location identifier, you can import the file into a specific location using the Location Manager upload page.

## File extensions

WinRiver II HydroML files must have a `.xml` file extension, otherwise AQUARIUS will not attempt to parse the file as a WinRiver measurement.

## Sample file

Click [here](./WinRiverSample.xml) to view/download a sample file.

## Reference

See the [WinRiver II home page]() for more details.
