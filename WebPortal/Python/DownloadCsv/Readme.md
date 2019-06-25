# Consume the WebPortal API from Python

This example shows how to consume the `GET /api/v1/export-data-set` to download time-series data and create a CSV.

- The example works in both Python 3.x and legacy Python 2.7.x. (Make the switch to Python 3.x [before it's too late!](https://pythonclock.org/) 

```bash
$ pip install requests
```

The only dependency is the ubiquitous `requests` package, for making HTTP requests.

## Configuring the WebPortal to allow API access

Since AQUARIUS WebPortal is an internet-facing product, API access is not automatically granted to anyone with a WebPortal login.

All credentialed accounts which belong to the built-in "Administrators" security role will have API access enabled.

Your WebPortal administrator may need to configure a specific account with permissions to make API calls.

WebPortal API access requires AQUARIUS WebPortal credentials (ie. a dedicated WebPortal account, not a Windows Authentication or an external Google/Microsoft account)

1) Have your administrator create a credentialed account
2) Create a new Security Role for accounts with API access
3) Enable all the checkboxes in the "Access API" column of the security role
4) Add the accounts to this security role
5) Save the new security role

Now all the accounts in the role will be able to access the WebPortal API.

## Explore the WebPortal API

Append the `/api/v1/swagger-ui/` path to your WebPortal root to view the API's Swagger UI page, an API test page.

EG. If your portal is hosted at `https://myserver/AQWebPortal`, then browse to `https://myserver/AQWebPortal/api/v1/swagger-ui/` to see the Swagger UI page.

## This Python script uses the `GET /export/data-set` API operation

The `GET /export/data-set` will export a range of time-series points from a single time-series.

- A `DataSet` identifier is required, using "Parameter.Label@Location" syntax. All other parameters are optional. 
- An optional time-range can be specified with a `StartTime` or `EndTime`, or with a `DateRange` value like 'Days30'
- An optional `Unit` parameter can request the values be converted to the specified unit.
- An optional `Timezone` parameter can request that all time-stamps be adjusted into the specified UTC-offset.
- An optional `PreProcessing` instruction can perform additional computation on the values.

## Running the script

You can invoke the python script from a command line, with named command line arguments.

The output of the script will be a CSV of time/value pairs.

If no CSV filename is given on the command line, the CSV will be written to standard out.

```bash
$ python download_csv.py -s https://myserver/AQWebPortal -u fred -p sekret -d Stage.Primary@Location1 --startTime 2019-06-01
Time,Value
2019-06-01T00:00:00.0000000+00:00,33400
2019-06-01T00:15:00.0000000+00:00,33800
2019-06-01T00:30:00.0000000+00:00,33600
...
```

To save the above output to the `points.csv`, just add the filename to the end of the command line:

```bash
$ python download_csv.py -s https://myserver/AQWebPortal -u fred -p sekret -d Stage.Primary@Location1 --startTime 2019-06-01 points.csv
Writing 1095 points to points.csv
```

## Error handling

The script follows standard error-handling conventions.

- When successful, the script exits with an error code of 0.
- If any error is detected, the script exits with a non-zero error code.

This pattern allows for easy integration with bash, Powershell, or CMD.EXE shells to check for errors.

```bash
$ python download_csv.py -s https://myserver/AQWebPortal -u fred -p WRONGsekret -d Stage.Primary@Location1 || echo "ERROR: Something failed!"
401 Client Error: Invalid UserName or Password for url: https://myserver/AQWebPortal/api/v1/export/data-set?DataSet=Stage.Primary%40Location1
ERROR: Something failed!
``` 

## Using `@args.txt` files to store common arguments

The script supports [`@args.txt`](https://github.com/AquaticInformatics/examples/wiki/Common-command-line-options#use-the-filenameext-syntax-to-specify-options-in-a-text-file) files for storing common command line arguments.

- One line per argument
- Blank lines are ignored.
- Lines starting with # are ignored as comment lines.

So you could store the credentials in a file called `connectioninfo.txt` that looks like this:

```text
# Each argument on a separate line. Blank lines and comments are ignored.
--server
https://myserver/AQWebPortal
 
--username
fred
 
--password
sekret
```


```bash
$ python download_csv.py @connectioninfo.txt -d Stage.Primary@Location1 --startTime 2019-06-01
Time,Value
2019-06-01T00:00:00.0000000+00:00,33400
2019-06-01T00:15:00.0000000+00:00,33800
2019-06-01T00:30:00.0000000+00:00,33600
...
```

## The `--help` page

```
$ python download_csv.py --help
usage: download_csv.py [-h] -s SERVER [-u USERNAME] [-p PASSWORD] -d DATASET
                       [--dateRange DATERANGE] [--startTime STARTTIME]
                       [--endTime ENDTIME] [--unit UNIT] [--timezone TIMEZONE]
                       [--preProcessing PREPROCESSING]
                       [outfile]

Download a CSV for a range of time-series data

positional arguments:
  outfile

optional arguments:
  -h, --help            show this help message and exit
  -s SERVER, --server SERVER
                        WebPortal server URL
  -u USERNAME, --username USERNAME
  -p PASSWORD, --password PASSWORD
  -d DATASET, --dataSet DATASET
                        The dataset identifier as "Parameter.Label@Location"
  --dateRange DATERANGE
  --startTime STARTTIME
  --endTime ENDTIME
  --unit UNIT           Override the time-series unit
  --timezone TIMEZONE   Override the time-series UTC offset
  --preProcessing PREPROCESSING
```

