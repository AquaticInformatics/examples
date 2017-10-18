# AQTS-FFA-curves.r
#
# Code for plotting annual peak flow series on
# extreme-value (Gumbel) paper.
#
# Touraj 2017-Sep-12
##############################################################################################

require(zoo)
require(mgcv)

# clean out workspace
rm(list = ls())

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Discharge.ft^3/s.comp.ref@88061040",                # The time-series to analyze
  historicalPeriodStartYear = 2010, historicalPeriodDurationYears = 4,  # The historical period to analyze
  eventPeriodStartDay = "2011-11-01", eventPeriodEndDay = "2011-12-31", # The event period to analyze
  cachedHistoricalDataPath = "ffaData.rda")                             # When set, use the data in this file to avoid a lengthy recalculation

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
fromEventPeriodStart = paste0(config$eventPeriodStartDay, startOfDay, utcOffset)
toEventPeriodEnd = paste0(config$eventPeriodEndDay, endOfDay, utcOffset)
eventPeriodLabel = sprintf("%s - %s", config$eventPeriodStartDay, config$eventPeriodEndDay)

historicalPeriodLabel = sprintf("%04d - %04d", config$historicalPeriodStartYear, config$historicalPeriodStartYear + config$historicalPeriodDurationYears - 1)

# Load the file when it exists to avoid a long computation
# Save the file when configured and it doesn't exist
loadHistoricalFile = !is.null(config$cachedHistoricalDataPath) && file.exists(config$cachedHistoricalDataPath)
saveHistoricalFile = !is.null(config$cachedHistoricalDataPath) && !file.exists(config$cachedHistoricalDataPath) && config$cachedHistoricalDataPath != ""

peakQ = numeric(config$historicalPeriodDurationYears)

if (loadHistoricalFile) {
  # Load the existing historical file
  load(file = config$cachedHistoricalDataPath)

} else {
  # Recompute the the historical data  
  for (iYears in 1:config$historicalPeriodDurationYears) {
    yearn = config$historicalPeriodStartYear + iYears - 1

    print(sprintf("processing %s ...", toString(yearn)))
    qfrom = sprintf("%04d-01-01%s%s", yearn, startOfDay, utcOffset)
    qto = sprintf("%04d-12-31%s%s", yearn, endOfDay, utcOffset)
    print(sprintf("From: %s", qfrom))
    print(sprintf("To: %s", qto))

    #read time-series from AQ API
    json <- timeseries$getTimeSeriesData(c(config$timeSeriesName),
                                        queryFrom = qfrom,
                                        queryTo = qto)

    if (json$NumPoints > 0) {
      # convert AQTS timestamps to POSIXct values
      timeStamp2 = sapply(json$Points$Timestamp, timeseries$parseIso8601)
      TS <- zoo(json$Points$NumericValue1, timeStamp2)

      peakQ[iYears] = TS[which.max(TS)]
    } else {
      # No data
      peakQ[iYears] = 0
    }

    print(peakQ)
  }
}

if (saveHistoricalFile) {
  # Save the historical file
  save(peakQ, file = config$cachedHistoricalDataPath)
}

####################################################################################################
print("Calculate Qpeak for the given period")

#read time-series from AQ API
json = timeseries$getTimeSeriesData(c(config$timeSeriesName),
                                    queryFrom = fromEventPeriodStart,
                                    queryTo = toEventPeriodEnd)

overlayQ = 0

if (json$NumPoints > 0) {
  # convert AQTS timestamps to POSIXct values
  timeStamp2 = sapply(json$Points$Timestamp, timeseries$parseIso8601)
  TS <- zoo(json$Points$NumericValue1, timeStamp2)    

  overlayQ = TS[which.max(TS)]
}

print("Qpeak for the given event period")
print(sprintf("From: %s", fromEventPeriodStart))
print(sprintf("To: %s", toEventPeriodEnd))
print(overlayQ)
###################################################################################################  

print("Done")
readline(prompt="Press [enter] to plot FFA curves...")

################################################################################################
################################################################################################

# Specify flows
#manual example for testing:
#Q = c(1.23,2.37,0.085,1.69,1.2,0.898,0.176,0.96,0.212,0.266)

#read from array
Q = peakQ

# Generate plotting positions
n = length(Q)
r = n + 1 - rank(Q)  # highest Q has rank r = 1
Ti = (n + 1)/r


# Set up x axis tick positions and labels
Ttick = c(1.001, 1.01, 1.1, 1.5, 2,  3,  4, 5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100, 200, 300, 400, 500)
xtlab = c(1.001, 1.01, 1.1, 1.5, 2, NA, NA, 5, NA, NA, NA, NA, 10, NA, NA, NA, NA, 15, NA, NA, NA, NA, 20, NA, 30, NA, NA, NA, 50, NA, NA, NA, NA, 100, 200, 300, 400, 500)
y = -log(-log(1 - 1/Ti))
ytick = -log(-log(1 - 1/Ttick))
xmin = min(min(y),min(ytick))
xmax = max(ytick)


# Fit a line by method of moments, along with 95% confidence intervals
KTtick = -(sqrt(6)/pi)*(0.5772 + log(log(Ttick/(Ttick-1))))
QTtick = mean(Q) + KTtick*sd(Q) 
nQ = length(Q)
se = (sd(Q)*sqrt((1+1.14*KTtick + 1.1*KTtick^2)))/sqrt(nQ) 
LB = QTtick - qt(0.975, nQ - 1)*se
UB = QTtick + qt(0.975, nQ - 1)*se
max = max(UB)
Qmax = max(QTtick)

windows()
# Plot peak flow series with Gumbel axis
plot(y, Q,
ylab = sprintf("Peak Flow (%s)", json$TimeSeries$Unit[1]),
     xaxt = "n", xlab = "T (yr)",
     ylim = c(0, Qmax),
     xlim = c(xmin, xmax),
     pch = 21, bg = "white",
     main = paste("Flood frequency analysis curve for", locationData$Identifier)
)  
par(cex = 0.65)
axis(1, at = ytick, labels = as.character(xtlab))


# Add fitted line and confidence limits
lines(ytick, QTtick, col = "black",lwd=2,)  
lines(ytick, LB, col = "black", lty = 3, lwd=2)
lines(ytick, UB, col = "black", lty = 3, lwd=2)  


# Draw grid lines
abline(v = ytick, lty = 3)             
abline(h = seq(0, floor(Qmax), 1000), lty = 3)             
#par(cex = 1)             

abline(h = overlayQ , lty = 1, col = "red", lwd=2) 

par(cex = 0.75)
legend("bottomright", bg = "white",
    lwd = c(2, 2, 2, 2, 1, 1),
    pch = c(NaN, NaN, NaN, NaN, 1, NaN),
    lty = c(1, NaN, 1, 3, NaN, NaN),
    col = c("red", NaN, "black", "black", "black", NaN),
    legend = c(
      "Observed peak flow event during:",
      eventPeriodLabel,
      "Expected prob.",
      "Confidence bound",
      "Historical peak flow during:",
      historicalPeriodLabel))


##############################################################################################
##############################################################################################

end.time <- Sys.time()
print(end.time)
time.taken <- end.time - start.time
print(time.taken)
