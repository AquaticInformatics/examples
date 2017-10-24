## Tableau Connector for AQUARIUS Time-Series

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FTableauConnector)


## Pre-requisites
- AQUARIUS Time-Series 2017.3+ installed on an app server
- Admin rights to the AQTS app server
- Tableau Desktop or Tableau Public, v10.1+

## Installation of the Tableau Connector

The Tableau connector is a set of static HTML and Javascript files, to be deployed on the AQTS app server as an IIS virtual folder in the `/TableauConnector/` route.

Extract the `TableauConnector.zip` archive to  `C:\inetpub\wwwroot\TableauConnector` on the AQTS app server. IIS will automatically detect the folder underneath `wwwroot` and serve up its content from the `/TableauConnector/` route.

## Using the connector from within Tableau

From Tableau Desktop:
- Select `Connect` => `Web Data Connector` and browse to `http://yourserver/TableauConnector`
- Enter your credentials and click the `Log In` button
- Click the `Get Locations` to load all the locations (or filter by location path)
- Select a location from the `Locations:` list
- Select one or more time-series from the `Time Series:` list and click the `Add-->` button
- Repeat the time-series selection sequence for other locations if desired
- Enter a start time in the `From Time:` selector
- Click the `Fetch!` button to load the data from the selected time-series into Tableau

Now you can explore your data within Tableau!
