# SharpShooterReportsRunner.exe

This console app can be used to load data from an AQTS system or external sources and render the report using a SharpShooter Reports template.

This utility is intended to help AQUARIUS Time-Series 3.x customers convert their custom reports from the 3.X code base to work on the 20xx codebase of AQTS.

## Features

- Can be easily scripted, run on a schedule.
- Reports a non-zero exit code if something goes wrong.
- Logs its activity to the standard `%ProgramData%\Aquatic Informatics\AQUARIUS\Logs` folder in the `SharpShooterReportsRunner.log` file.
- Can upload a rendered PDF as an external report to AQTS.
- Can launch the SharpShooter Reports designer to edit the template in context with your AQTS data.

## Requirements

- .NET 4.7 runtime
- A valid SharpShooter Reports license for 7.1.1.18 or greater. Contact support@aquaticinformatics.com to see if your account is eligible for a free license.
- Credentials for an AQTS 20xx app server

## Operation

- Run `SharpShooterReportsRunner /help` to see all the command line options.
- Add as many `TimeSeries=identifier` options as needed. Each time-series will be loaded into a separate dataset named `TimeSeriesX`, with `X` starting at 1.
- Add the `/LaunchReportDesigner=true` option to launch the SharpShooter Report designer GUI along with your time-series data.
- Your report may require many command-line options. Consider using the `@optionsFile` syntax to store options in a text file, one line per option.
