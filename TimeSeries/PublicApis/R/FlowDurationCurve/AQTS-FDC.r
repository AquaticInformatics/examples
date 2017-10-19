# AQTS-FDC.r
#
# code to conduct flow-duration analysis from AQUARIUS data
# generates two graphs:
#  - hydrograph plot of daily flow
#  - flow-duration curve
#
# Touraj Farahmand 2017-10-1
##############################################################################################


require(hydroTSM)
require(zoo)
require(Rcpp)
require(mgcv)

# clean out workspace
rm(list = ls())   

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Discharge.ft^3/s.comp.ref@88061040",                # The time-series to analyze
  eventPeriodStartDay = "2011-01-01", eventPeriodEndDay = "2011-12-31", # The event period to analyze
  uploadedReportTitle = "Flood Duration Curve",                         # The title of the uploaded report
  removeDuplicateReports = TRUE)                                        # Set to TRUE to avoid duplicate reports in WebPortal

# Set timer to measure execution time
start.time <- Sys.time()
print(start.time)

# Load supporting code
source("timeseries_client.R")

# Connect to the AQTS server
timeseries$connect(config$server, config$username, config$password)

# Get the location data
locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(config$timeSeriesName))
utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)

startOfDay = "T00:00:00"
endOfDay = "T23:59:59.9999999"

# event period
fromPeriodStart = paste0(config$eventPeriodStartDay, startOfDay, utcOffset)
toPeriodEnd = paste0(config$eventPeriodEndDay, endOfDay, utcOffset)
periodLabel = sprintf("%s - %s", config$eventPeriodStartDay, config$eventPeriodEndDay)

# Optionally upload results to AQTS
saveToPdf = !is.null(config$uploadedReportTitle) && config$uploadedReportTitle != ""
removeDuplicateReports = saveToPdf && !is.null(config$removeDuplicateReports) && config$removeDuplicateReports

#read time-series from AQ API
json <- timeseries$getTimeSeriesData(c(config$timeSeriesName),
                                     queryFrom = fromPeriodStart,
                                     queryTo = toPeriodEnd)

if (json$NumPoints > 0) {
  # convert AQTS timestamps to POSIXct values
  # Normally we'd prefer this transform, to ensure correct ISO timezone parsing: 
  # timeStamp2 = sapply(json$Points$Timestamp, timeseries$parseIso8601)
  # But the above transform causes this stop error within subdaily2daily : Error in prettyNum(.Internal(format(x, trim, digits, nsmall, width, 3L,  : invalid 'trim' argument
  # So instead, just chop off the timezone component. The timestamps will still be correct for our analysis.
  timeStamp2 = strptime(substr(json$Points$Timestamp,0,19), "%FT%T")
  TS = zoo(json$Points$NumericValue1, timeStamp2)
  
  #calculate daily from original frequency 
  TSD = subdaily2daily(TS, FUN=mean, na.rm = TRUE)
  
  #plot daily flow hydrograph
  windows()
  flowPlotTitle = paste("Daily Flow for", locationData$LocationName, "Site during:\n", periodLabel)
  plot(TSD, main = flowPlotTitle , xlab = "Time", ylab = sprintf("Flow (%s)", json$TimeSeries$Unit[1]))
  
  if (saveToPdf) {
    # Upload the results to AQTS as a PDF
    localPathToPdf = "DailyFlow.pdf"
    dev.copy2pdf(width = 7, file = localPathToPdf, out.type = "pdf")

    timeseries$uploadExternalReport(locationData, localPathToPdf, config$uploadedReportTitle, removeDuplicateReports)
  }
  
  #override tick labels for y axis
  ytick = c(1,2,5,10,20,30,40,50,70,100,150,200)
  
  #call fdc from hydroTSM package to plot FDC
  windows()
  fdcPlotTitle = paste("Flow-Duration curve for", locationData$LocationName, "Site during:\n", periodLabel)
  fdc(TSD, main = fdcPlotTitle, xlab = "Percentage Exceedance", ylab = sprintf("Flow (%s)", json$TimeSeries$Unit[1]) , yat = ytick,panel.first = grid())
  
} else {
  # No data
  print(c("No data found during:",periodLabel))
}

#processing end time and elapsed time
end.time <- Sys.time()
print(end.time)
time.taken <- end.time - start.time
print(time.taken)

if (saveToPdf) {
  # Upload the results to AQTS as a PDF
  localPathToPdf = "FlowDurationCurve.pdf"
  dev.copy2pdf(width = 7, file = localPathToPdf, out.type = "pdf")

  timeseries$uploadExternalReport(locationData, localPathToPdf, config$uploadedReportTitle, removeDuplicateReports)
}
