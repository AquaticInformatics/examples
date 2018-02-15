# Sample HFC field data file

AQUARIUS Time-Series can import field data from Hydrometric Field Computer files, used by Water Survey Canada.

## AQUARIUS location identifiers

The `STN.NUM` field should contain the AQUARIUS location identifier in order to support automatic upload from Springboard.

If the file does not contain an AQUARIUS location identifier, you can import the file into a specific location using the Location Manager upload page.

## File extensions

HFC files must have a file extension matching `*.dis` or `*.mq`, otherwise AQUARIUS will not attempt to parse the file as a HFC measurement.

## Sample file
Click [here](./HfcSample.MQ1) to view/download a sample file.
