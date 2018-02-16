# Sample Scottech STIL field data file

AQUARIUS Time-Series can import field data from Scottech STIL gauging files.

## AQUARIUS location identifiers

The first line after the `@Gauging` marker should contain the AQUARIUS location identifier in order to support automatic upload from Springboard.

If the file does not contain an AQUARIUS location identifier, you can import the file into a specific location using the Location Manager upload page.

## File extensions

Scottech STIL files must have a `.glr` file extension, otherwise AQUARIUS will not attempt to parse the file as a Scottech measurement.

## Sample file

Click [here](./ScottechSample.glr) to view/download a sample file.

## Reference

See the [Scottech STIL home page](http://www.scottech.net/faq/hydro/current_meters_accessories/stil_river_gauging_logger_glogg/) for more details.
