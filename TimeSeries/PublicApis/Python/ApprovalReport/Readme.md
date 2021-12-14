# Approval Report

This script will inspect the approval levels of time-series, rating models, and field visits,
and create a CSV report of the approval status by location.

- Defaults to all locations, but can be filtered to specific location or location folder
- Defaults to all time, but can be filtered to a specific time-range
- Will upload a location attachment named "LocationApprovalReport.csv" to each location

## Format of the CSV output

```csv
LocationIdentifier, Type, Identifier, Approval Level, StartTime, EndTime, Description
``` 