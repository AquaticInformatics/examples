# AQTS-WindRose.R
#
# Plots a wind rose diagram from a wind-direction and wind-speed pair of time-series
#

# clean out workspace
rm(list = ls())

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  windSpeedSeriesName = "Wind speed.mph.Work@01372058",                 # The wind-speed time-series name
  windDirectionSeriesName = "Wind direction.deg.Work@01372058",         # The wind-direction time-series name
  eventPeriodStartDay = "2011-01-01", eventPeriodEndDay = "2011-12-31", # The event period to analyze
  uploadedReportTitle = "Wind Rose",                                    # The title of the uploaded report
  removeDuplicateReports = TRUE)                                        # Set to TRUE to avoid duplicate reports in WebPortal

# Load supporting code
source("wind_rose_ggplot.R")
source("timeseries_client.R")

# Connect to the AQTS server
timeseries$connect(config$server, config$username, config$password)

# Get the location data
locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(config$windSpeedSeriesName))
utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)

startOfDay = "T00:00:00"
endOfDay = "T23:59:59.9999999"

# event period
fromEventPeriodStart = paste0(config$eventPeriodStartDay, startOfDay, utcOffset)
toEventPeriodEnd = paste0(config$eventPeriodEndDay, endOfDay, utcOffset)
eventPeriodLabel = sprintf("%s - %s", config$eventPeriodStartDay, config$eventPeriodEndDay)

# Optionally upload results to AQTS
saveToPdf = !is.null(config$uploadedReportTitle) && config$uploadedReportTitle != ""
removeDuplicateReports = saveToPdf && !is.null(config$removeDuplicateReports) && config$removeDuplicateReports

# Grab the wind data for the event period
# Make sure we force the speed units into meters-per-second, since that is the assumed units within the plot.windrose() method
json = timeseries$getTimeSeriesData(
      c(config$windSpeedSeriesName, config$windDirectionSeriesName),
      outputUnitIds = c("m/s", ""),
      queryFrom = fromEventPeriodStart,
      queryTo = toEventPeriodEnd)

# Plot the wind rose for the event period
plot.windrose(spd = json$Points$NumericValue1, dir = json$Points$NumericValue2)

if (saveToPdf) {
  # Upload the results to AQTS as a PDF
  localPathToPdf = "WindRose.pdf"
  ggsave(localPathToPdf, width = 7)

  timeseries$uploadExternalReport(locationData, localPathToPdf, config$uploadedReportTitle, removeDuplicateReports)
}
