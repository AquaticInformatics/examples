## SdkExamples.sln

Requires: Visual Studio 2015+ (Community Edition is fine)

The SdkExamples solution includes some example programs using the [Aquarius.SDK for .NET](https://github.com/AquaticInformatics/aquarius-sdk-net).

### Common patterns for .NET console apps

You'll notice a number of common patterns in these sample console apps:
- The app will exit with a zero exit code if successful, or with a positive exit code if an error occurs. This allows you to easily script the apps from a batch file and still check for errors.
- The app will stop on the first error encountered.
- Program arguments are `Name=value` pairs parsed by a case-insensitive regular expression. These arguments can be easily supplied in a CMD.EXE window as `/Name=value` or in a bash shell as `-name=value`, depending on your choice of programming shell.
- Running the app without any arguments will display a help message.
- Most apps can be run directly from a console on the AQTS app server, and will connect to `localhost` using the default admin account. Specify `Server`, `Username`, and `Password` arguments to connect using different credentials.
- Time-series can be specified using identifier strings (ie. `Stage.Logger@Location`) or using unique ID (GUID) values. 

### Inspecting REST API traffic using Fiddler

Running these console apps with the popular [Fiddler](http://www.telerik.com/fiddler) web debugger capturing traffic in the background can be a useful exercise to see the flow of REST API requests and responses between these client apps and your AQTS server.

### AppendPoints

The `AppendPoints` example app will queue some generated points to be appended to a time-series using the Acquisition API and will wait until the append has successfully completed.

The number, value, and timing of the generated points are controlled by optional arguments. By default, one point will be appended with a value of 1.0 at the current time.

Key concepts demonstrated:
- Using the `GET /Publish/v2/GetTimeSeriesDescriptionList` request to resolve the unique ID of a time-series identifier
- Using the `POST /Acquisition/v2/{UniqueId}/append` request to queue points to be appended
- Using the `GET /Acquisition/v2/timeseries/appendstatus/{AppendRequestIdentifier}` request to poll the status of an append job
- Using the Aquarius.SDK's [`RequestAndPollUntilComplete()`](https://github.com/AquaticInformatics/aquarius-sdk-net/wiki/Adaptive-polling#adaptive-polling-via-requestandpolluntilcomplete) method to efficiently poll an AQTS server.

### ExternalProcessor

The `ExternalProcessor` example app will implement an external processor that:
- Monitors a time-series for any changes (using a configurable polling interval)
- When a change is detected, retrieves all the changed point values and timestamps from AQTS
- Calculates new timestamps and values using the retrieved time-series points as inputs (this is the "external processing" part)
- POSTs the calculated values back to a reflected time-series in AQTS

The "external processing" performed in this example app is trivial:
- Add 30 seconds to every timestamp of the input time-series points
- Square the value of the input time-series point
- Preserve any gaps (ie. where `TimeSeriesPoint.Value == null`)

To see the flow of how this might work, use two separate shell windows (CMD.EXE or bash):
- In the first window, run the `ExternalProcessor` app to monitor time-series A, and append its calculated results to time-series B.
- In the second window, run the `AppendPoints` app to append a single point to time-series A. When a new point is appended, you should see the `ExternalProcessor` detect the new point and recalculate the changed values.

Key concepts demonstrated: Everything from the `AppendPoints` example, plus:
- Using the `GET /Publish/v2/GetTimeSeriesUniqueIdList` request to monitor a time-series for ["changes since"](https://github.com/AquaticInformatics/aquarius-sdk-net/wiki/Monitoring-changes-to-a-time-series#changes-since-concept) the last time you polled the system.
- Using the `POST /Acquisition/v2/{UniqueId}/reflected` request to queue points to be appended to a reflected time-series, overwriting any existing points within a time range.
