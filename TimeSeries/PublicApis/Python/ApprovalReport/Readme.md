# Approval Report

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FPython%2FApprovalReport)

This script will inspect the approval levels of time-series, rating models, and field visits,
and create a CSV report of the approval status by location.

- Defaults to all locations, but can be filtered to specific location or location folder
- Defaults to all time, but can be filtered to a specific time-range
- Will upload a location attachment named "LocationApprovalReport.csv" to each location
- Can apply any list of tags to the uploaded reports, useful for exposing the reports in WebPortal.

## Format of the CSV output

```csv
LocationIdentifier, Approval Level, StartTime, EndTime, Type, Identifier, Description
LOC55, Working, 2019-10-01, 2021-04-01, TimeSeries, Stage.Working@LOC55, 28756 points
LOC55, Working, 2020-05-15, 2020-07-18, TimeSeries, Air Temp.Telemetry@LOC55, 3120 points
LOC55, Working, 2017-01-01, 2019-01-01, RatingModel, Stage-Discharge.Rating@LOC55, "Curves 001, 002"
LOC55, Working, 1995-09-27, 2000-01-01, FieldVisit, 16 visits,  "24 discharge measurements, 85 readings"
``` 

## Running the report for an entire system can take many hours

The report examines every time-series point, rating model, and field visit in each selected location.
Since the report defaults to examining every location in your system, a complete run might take a few days to complete.

You can use the `--location=MyLocation` or `--locationFolder="My Root.Sub folder1.Subfolder2` options to reduce the number of locations examined.

In sample test runs, a system with 2400 locations, 26000 time-series, 140 rating models, and 35000 field visits took 23 hours to generate all the reports.
 
## Help screen
```
$ python approval_report.py -h
usage: approval_report.py [-h] [--server SERVER] [--username USERNAME] [--password PASSWORD] [--location LOCATION]
                          [--locationFolder LOCATIONFOLDER] [--queryFrom QUERYFROM] [--queryTo QUERYTO] [--reportFilename REPORTFILENAME]
                          [--keepDuplicates] [--tags TAGS]

options:
  -h, --help            show this help message and exit
  --server SERVER       The AQTS app server (default: localhost)
  --username USERNAME   The AQTS username (default: admin)
  --password PASSWORD   The AQTS password (default: admin)
  --location LOCATION   Filter to this one location. Defaults to all locations if omitted. (default: None)
  --locationFolder LOCATIONFOLDER
                        Filter to this one location folder. Defaults to all locations if omitted. (default: None)
  --queryFrom QUERYFROM
                        Filter results to approvals after this date/time. Defaults to the start of record. (default: None)
  --queryTo QUERYTO     Filter results to approvals before this date/time. Defaults to the start of record. (default: None)
  --reportFilename REPORTFILENAME
                        The name of generated report file in each location (default: LocationApprovalReport)
  --keepDuplicates      When set, duplicate report files will be kept at each location (default: False)
  --tags TAGS           A comma-separated list of tag:value pairs to apply to the uploaded report. (default: None)
```