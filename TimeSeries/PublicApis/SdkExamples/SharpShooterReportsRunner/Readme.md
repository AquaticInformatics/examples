# SharpShooterReportsRunner.exe

This console app can be used to load data from an AQTS system or external sources and render the report using a SharpShooter Reports template.

This utility is intended to help AQUARIUS Time-Series 3.x customers convert their custom reports from the 3.X code base to work on the 20xx codebase of AQTS.

## Features

- Can be easily scripted, run on a schedule.
- Reports a non-zero exit code if something goes wrong.
- Logs its activity to the standard `%ProgramData%\Aquatic Informatics\AQUARIUS\Logs` folder in the `SharpShooterReportsRunner.log` file.

## Requirements

- .NET 4.7 runtime
- A valid SharpShooter Reports license for 7.1.1.18 or greater. Contact support@aquaticinformatics.com to see if your account is eligible for a free license.
- Credentials for an AQTS 20xx app server
