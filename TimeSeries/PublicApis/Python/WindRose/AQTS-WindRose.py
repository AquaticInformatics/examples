# Load the required dependencies
import pandas as pd
import matplotlib.pyplot as plt
from windrose import WindroseAxes
from timeseries_client import timeseries_client

# Configure your data here
config = dict(
    server='doug-vm2012r2', username='admin', password='admin',         # AQTS credentials for your server
    windSpeedSeriesName="Wind Vel.Work@01372058",                       # The wind-speed time-series name
    windDirectionSeriesName="Wind Dir.Work@01372058",                   # The wind-direction time-series name
    eventPeriodStartDay="2011-01-01", eventPeriodEndDay="2011-12-31",   # The event period to analyze
    uploadedReportTitle="Wind Rose",                                    # The title of the uploaded report
    removeDuplicateReports=True)                                        # Set to True to avoid duplicate reports in WebPortal

# Connect to AQTS
with timeseries_client(config['server'], config['username'], config['password']) as timeseries:
    tsSpeed = timeseries.getTimeSeriesUniqueId(config['windSpeedSeriesName'])
    tsDir = timeseries.getTimeSeriesUniqueId(config['windDirectionSeriesName'])

    # Grab the data from AQTS
    json = timeseries.publish.get("/GetTimeSeriesData", params=dict(
        TimeSeriesUniqueIds=[tsSpeed, tsDir],
        TimeSeriesOutputUnitIds=['m/s', ''],
        QueryFrom=config['eventPeriodStartDay'],
        QueryTo=config['eventPeriodEndDay']
    ))

    # Throw the points in a Pandas dataframe
    df = pd.DataFrame(json['Points'])

    # Extract the wind direction and speed
    windSpeed = df['NumericValue1']
    windDir = df['NumericValue2']

    # Build the windrose graph
    ax = WindroseAxes.from_ax()
    ax.bar(windDir, windSpeed, normed=True, opening=0.8, edgecolor='white')
    ax.set_legend()

    if config.has_key('uploadedReportTitle') and len(config['uploadedReportTitle']) > 0:
        # Save the file locally
        localPdfPath = "windrose.pdf"
        plt.gcf().savefig(localPdfPath, bbox_inches='tight')

        # Get the location data
        locationData = timeseries.getLocationData(json['TimeSeries'][0]['LocationIdentifier'])

        # Upload the file
        uploadedReport = timeseries.uploadExternalReport(
            locationData['UniqueId'],
            localPdfPath,
            config['uploadedReportTitle'],
            config['removeDuplicateReports'])

    # Show the graph
    plt.show()


# And we're done
