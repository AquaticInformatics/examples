# AQTS-idf-curves.r
#
# code to conduct idf analysis
# generates two graphs:
#  - graphs of i vs T for each duration
#  - plot of i vs. D for a range of return periods
#  - Overlay intensity for a given period on the graph
#
# Touraj 2017-July-11
##############################################################################################

require(zoo)
require(mgcv)

# clean out workspace
rm(list = ls())  

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Precipitation.Historic@DEMO_01",                    # The time-series to analyze
  historicalPeriodStartYear = 1995, historicalPeriodDurationYears = 4,  # The historical period to analyze
  eventPeriodStartDay = "1995-11-01", eventPeriodEndDay = "1995-12-31", # The event period to analyze
  uploadedReportTitle = "IDF Plot",                                     # The title of the uploaded report
  removeDuplicateReports = TRUE,                                        # Set to TRUE to avoid duplicate reports in WebPortal
  cachedHistoricalDataPath = "idfData.rda")                             # When set, use the data in this file to avoid a lengthy recalculation

# Set timer to measure execution time
start.time <- Sys.time()
print(start.time)

# Load supporting code
source("idf-analysis.R")
source("timeseries_client.R")  

# Connect to the AQTS server
timeseries$connect(config$server, config$username, config$password)

# Get the location data
locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(config$timeSeriesName))
utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)

startOfDay = "T00:00:00"
endOfDay = "T23:59:59.9999999"

# event period
fromEventPeriodStart = paste0(config$eventPeriodStartDay, startOfDay, utcOffset)
toEventPeriodEnd = paste0(config$eventPeriodEndDay, endOfDay, utcOffset)
eventPeriodLabel = sprintf("%s - %s", config$eventPeriodStartDay, config$eventPeriodEndDay)

# Load the file when it exists to avoid a long computation
# Save the file when configured and it doesn't exist
loadHistoricalFile = !is.null(config$cachedHistoricalDataPath) && file.exists(config$cachedHistoricalDataPath)
saveHistoricalFile = !is.null(config$cachedHistoricalDataPath) && !file.exists(config$cachedHistoricalDataPath) && config$cachedHistoricalDataPath != ""

durations = c(5/60, 10/60, 15/60, 30/60, 1, 2, 6, 12, 24)   # durations in hr for idf analysis
samplingRate = 5/60  #sampling rate for the data
Tp = c(2, 5, 10, 25, 50, 100, 500)   #return periods

nd = length(durations)
depthMatrix = matrix(as.numeric(), nrow = config$historicalPeriodDurationYears, ncol = nd + 1, byrow = TRUE)

if (loadHistoricalFile) {
  # Load the existing historical file
  load(file = config$cachedHistoricalDataPath)

} else {
  # Recompute the the historical data
  for (iYears in 1:config$historicalPeriodDurationYears) {
    yearn = config$historicalPeriodStartYear + iYears - 1
    depthMatrix[iYears, 1] = yearn

    print(sprintf("processing %s ...", toString(yearn)))
    qfrom = sprintf("%04d-01-01%s%s", yearn, startOfDay, utcOffset)
    qto = sprintf("%04d-12-31%s%s", yearn, endOfDay, utcOffset)
    print(sprintf("From: %s", qfrom))
    print(sprintf("To: %s", qto))

    #read time-series from AQ API
    json = timeseries$getTimeSeriesData(c(config$timeSeriesName),
                                          queryFrom = qfrom,
                                          queryTo = qto)

    if (json$NumPoints == 0) {
      # No data, so skip
      next
    }

    # convert AQTS timestamps to POSIXct values
    timeStamp2 = sapply(json$Points$Timestamp, timeseries$parseIso8601)
    TS <- zoo(json$Points$NumericValue1, timeStamp2)

    windows()
    barplot(height = coredata(TS), names.arg = time(TS),
                      xlab = "Date", ylab = sprintf("%s (%s)", json$TimeSeries$Parameter[1], json$TimeSeries$Unit[1]))

    for (iduration in 1:nd) {
      TS2 = rollapply(TS, width = durations[iduration] / samplingRate, by = 1, FUN = sum, na.rm = TRUE, align = "left")
      depthMatrix[iYears, iduration + 1] = TS2[which.max(TS2)]
    }
    print(depthMatrix)
  }
}

if (saveHistoricalFile) {
  # Save the historical file
  save(depthMatrix, file = config$cachedHistoricalDataPath)
}

####################################################################################################
print("Calculate intensities for the given event period")

overlayIntensities = numeric(nd)

# Read the event period of the time-series
json = timeseries$getTimeSeriesData(c(config$timeSeriesName),
                                    queryFrom = fromEventPeriodStart,
                                    queryTo = toEventPeriodEnd)

if (json$NumPoints > 0) {
  # convert AQTS timestamps to POSIXct values
  timeStamp2 = sapply(json$Points$Timestamp, timeseries$parseIso8601)
  TS <- zoo(json$Points$NumericValue1, timeStamp2)    

  for(iduration in 1:nd)  {
    TS2 = rollapply(TS, width = durations[iduration]/samplingRate, by = 1, FUN = sum, na.rm = TRUE, align = "left")
    overlayIntensities[iduration] = TS2[which.max(TS2)]
  }
}

print("Intensities for the given event period")
print(sprintf("From: %s",fromEventPeriodStart))
print(sprintf("To: %s",toEventPeriodEnd))
print(overlayIntensities)
###################################################################################################  

#call idfAnalysis function

#plot on screen
isPdf = FALSE # Set to TRUE to save PDF
IdfAnalysis(depthMatrix, overlayIntensities, durations, Tp, isPdf, locationData$Identifier, eventPeriodLabel, json$TimeSeries$Unit[1])

end.time <- Sys.time()
print(end.time)
time.taken <- end.time - start.time
print(time.taken)

# Optionally upload results to AQTS
saveToPdf = !is.null(config$uploadedReportTitle) && config$uploadedReportTitle != ""
removeDuplicateReports = saveToPdf && !is.null(config$removeDuplicateReports) && config$removeDuplicateReports

if (saveToPdf) {
  # Upload the results to AQTS as a PDF
  localPathToPdf = "IntensityDurationFrequencyPlot.pdf"
  dev.copy2pdf(width = 7, file = localPathToPdf, out.type = "pdf")

  timeseries$uploadExternalReport(locationData, localPathToPdf, config$uploadedReportTitle, removeDuplicateReports)
}
