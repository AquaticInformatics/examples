# Sample USGS HydroML field data file

AQUARIUS Time-Series can import field data from USGS HydromML 5.0 files.

## AQUARIUS location identifiers

The `<SiteIdentifier>` XML node should contain the AQUARIUS location identifier in order to support automatic upload from Springboard.

If the file does not contain an AQUARIUS location identifier, you can import the file into a specific location using the Location Manager upload page.

## File extensions

USGS HydroML files must have a `.xml` file extension, otherwise AQUARIUS will not attempt to parse the file as a USGS activity measurement.

## Sample file

Click [here](./UsgsHydroMLSample.xml) to view/download a sample file.

## Reference

See the [USGS HydroML home page](https://water.usgs.gov/XML/NWIS/5.0/index.html) for more details.
