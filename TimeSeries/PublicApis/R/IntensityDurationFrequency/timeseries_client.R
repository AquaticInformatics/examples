# Load dependencies
require(jsonlite)
require(httr)

# Sys.setenv(http_proxy="http://localhost:8888") # Enables Fiddler capturing of traffic
# Sys.setenv(http_proxy="") # Disables Fiddler proxying

# Create a simple AQUARIUS Time-Series API client.
timeseriesClient <- setRefClass("timeseriesClient",
  fields = list(
    version = "character",
    publishUri = "character",
    acquisitionUri = "character",
    provisioningUri = "character",
    legacyPublishUri = "character",
    legacyAcquisitionUri = "character",
    isLegacy = "logical"),
  methods = list(
#' Connects to an AQTS server
#' 
#' Once authenticated, all subsequent requests to the AQTS server will reuse the authenticated session
#' 
#' @param hostname A server name or IP address
#' @param username The AQTS credentials username
#' @param password The AQTS credentials password
#' @examples 
#' connect("localhost", "admin", "admin") # When running R on your AQTS app server
#' connect("myserver", "me", "mypassword") # Connect over the network
#' connect("https://myserver", "user", "letmein") # Connect to an AQTS server with HTTPS enabled
    connect = function(hostname, username, password) {
      # Auto-configure the proxy by default
      .self$configureProxy()

      # Support schemeless and schemed hosts for convenience
      prefix <- "http://"
      if (startsWith(hostname, "http://") || startsWith(hostname, "https://")) {
        url <- parse_url(hostname)
        hostname <- paste0(url$scheme, "://", url$hostname)
        prefix <- ""
      }

      # Grab the version of the AQTS server
      r <- GET(paste0(prefix, hostname, "/AQUARIUS/apps/v1/version"))
      stop_for_status(r, "detecting AQTS version")

      j <- fromJSON(content(r, "text"))
      version <<- j$ApiVersion
      
      # Anything earlier than 14.3 is considered legacy code
      isLegacy <<- .self$isVersionLessThan("14.3")

      # Compose the base URI for all API endpoints
      publishUri <<- paste0(prefix, hostname, "/AQUARIUS/Publish/v2")
      acquisitionUri <<- paste0(prefix, hostname, "/AQUARIUS/Acquisition/v2")
      provisioningUri <<- paste0(prefix, hostname, "/AQUARIUS/Provisioning/v1")
      
      if (isLegacy) {
        legacyPublishUri <<- paste0(prefix, hostname, "/AQUARIUS/Publish/AquariusPublishRestService.svc")
        legacyAcquisitionUri <<- paste0(prefix, hostname, "/AQUARIUS/AQAcquisitionService.svc")
      }

      # Try to authenticate using the supplied credentials
      credentials <- list(Username = username, EncryptedPassword = password)
      
      if (isLegacy) {
        # Authenticate via the older operation, so that a session cookie is set
        r <- GET(paste0(publishUri, "/GetAuthToken"), query = credentials)
      } else {
        # Authenticate via the preferred endpoint
        r <- POST(paste0(publishUri, "/session"), body = credentials, encode = "json")
      }
      stop_for_status(r, "authenticate with AQTS")
    },

#' Disconnects immediately from an AQTS server
    disconnect = function() {
      
      if (isLegacy) {
        # 3.X doesn't support proper disconnection, so just abandon the session below
      } else {
        # Delete the session immediately, like we should
        r <- DELETE(paste0(publishUri, "/session"))
        stop_for_status(r, "disconnect from AQTS")
      }
      
      # Abandon all session cookies associated with the connection
      handle_reset(publishUri)
    },

#' Auto-configures the proxy to route all requests through Fiddler
#'
#' This method configures the R proxy to route everything through Fiddler if it is running
#' 
#' Sys.setenv(http_proxy="http://localhost:8888") # Enables Fiddler capturing of traffic
#' Sys.setenv(http_proxy="") # Disables Fiddler proxying
    configureProxy = function() {
      if (length(Sys.getenv("R_DISABLE_FIDDLER")[0]) > 0 || Sys.info()['sysname'] != "Windows")
        return()

      # Check if Fiddler is running
      taskCheck <- system('tasklist /FI "IMAGENAME eq Fiddler.exe"', intern = TRUE)

      if (length(taskCheck) < 4)
        return()

      # Fiddler is running, so enable the proxy
      message("Auto-routing all web requests through Fiddler.")
      message('To disable this behaviour, call Sys.setenv(http_proxy="") or set the R_DISABLE_FIDDLER environment variable.')
      Sys.setenv(http_proxy = "http://localhost:8888")
    },

#' Determines if a target version string is strictly less than a source version
#' 
#' This method takes dotted version strings and compares them by numerical components.
#' It safely avoids the errors string comparison, which incorrectly says "3.10.510" > "17.2.123".
#' @param targetVersion Target version string
#' @param sourceVersion Optional source version string. If missing, use the connected server version
#' @return TRUE if the target version is strictly less than the source version
    isVersionLessThan = function(targetVersion, sourceVersion) {
      if (missing(sourceVersion)) {
        sourceVersion <- version
      }

      # Create the vectors of integers using this local sanitizing function
      createIntegerVector <- function(versionText) {

        if (versionText == "0.0.0.0") {
          # Force unreleased developer builds to act as latest-n-greatest
          versionText <- "9999.99"
        }

        # Convert the text into a vector of integers
        v <- as.integer(strsplit(versionText, ".", fixed = TRUE)[[1]])

        if (length(v) > 0 && v[1] >= 14 && v[1] <= 99) {
          # Adjust the leading component to match the 20xx.y release convention
          v[1] = v[1] + 2000
        }

        v
      }

      # Convert to vectors of integers
      target <- createIntegerVector(targetVersion)
      source <- createIntegerVector(sourceVersion)

      # Take the differnce of the common parts
      minlength <- min(length(target), length(source))

      diff <- head(target, minlength) - head(source, minlength)

      if (all(diff == 0)) {
        # All the common parts are identical
        length(source) < length(target)
      } else {
        # Assume not less than
        lessThan <- FALSE

        for (d in diff) {
          if (d < 0) {
            break
          } else if (d > 0) {
            lessThan <- TRUE
            break
          }
        }

        lessThan
      }
    },

#' Gets the unique ID of a time-series from its identifier string
#' 
#' If the input string is already a unique ID, the input value is simply returned unmodified.
#' 
#' @param timeSeriesIdentifier A time-series identifier in <Parameter>.<Label>@<LocationIdentifier> syntax
#' @return The unique ID of the time-series
#' @examples
#' getTimeSeriesUniqueId("Stage.Working@MyLocation") # cdf184928c8249abb872f852f0fa7d01
    getTimeSeriesUniqueId = function(timeSeriesIdentifier) {
      if (isLegacy | !grepl("@", timeSeriesIdentifier)) {
        # It's not in Param.Label@Location format, so just leave it as-is
        timeSeriesIdentifier
      } else {
        # Parse out the location identifier
        location <- .self$getLocationIdentifier(timeSeriesIdentifier)

        # Ask for all the time-series at that location
        r <- GET(paste0(publishUri, "/GetTimeSeriesDescriptionList"), query = list(LocationIdentifier = location))
        stop_for_status(r, paste("retrieve time-series at location", location))

        # Find the unique ID by matching the full identifier
        j <- fromJSON(content(r, "text"))
        uniqueId <- j$TimeSeries$UniqueId[which(j$TimeSeries$Identifier == timeSeriesIdentifier)]
        
        if (length(uniqueId) <= 0) {
          # Throw on the brakes
          stop("Can't find time-series '", timeSeriesIdentifier, "' in location '", location, "'.")
        }
        
        uniqueId
      }
    },

#' Gets the location identifier from a time series identifier string
#'
#' @param timeSeriesIdentifier A time-series identifier in <Parameter>.<Label>@<LocationIdentifier> syntax
#' @return The identifier of the location
#' @examples
#' getLocationIdentifier("Stage.Working@MyLocation") # MyLocation
    getLocationIdentifier = function(timeSeriesIdentifier) {
      if (!grepl("@", timeSeriesIdentifier)) {
        stop(timeSeriesIdentifier, " is not a <Parameter>.<Label>@<Location> time-series identifier")
      }

      strsplit(timeSeriesIdentifier, "@")[[1]][[2]]
    },

#' Parse an ISO 8601 timestamp into a POSIXct value
#' 
#' @param isoText An ISO 8601 timestamp string
#' @return The equivalent POSIXct datetime
#' @examples 
#' parseIso8601("2015-04-01T00:00:00Z") # April Fool's day, 2015 UTC
#' parseIso8601("2015-04-01T00:00:00-08:00") # April Fool's day, 2015, Pacific Standard Time
#'
#' times = sapply(json$Points$Timestamp, timeseries$parse8601) # Convert all JSON timestamp strings into POSIXct format
    parseIso8601 = function(isoText) {
      # Wow. Parsing true ISO 8061 timestamps (which AQTS outputs) in R is hard.
      #
      # Parsing them **efficiently** and correctly when dealing with thousands of points is even harder!
      #
      # There are many packages claiming to be compliant with ISO 8601.
      # But most are only compliant-ish(TM), failing to deal with timezones and UTC offsets
      #
      # R's strptime() method has a %z conversion specifier for timezone offsets.
      #
      # But %z only suports RFC 822 offset in: <sign><4-digits>
      #
      #  +HHMM (+1400 max)
      #  -HHMM (-1400 max)
      #
      # AQTS uses ISO 8601 offsets, either "Z" or <sign><2-digit-hour>:<2-digit-minute>
      #
      #  Z      (UTC assumed)
      #  +HH:MM (+14:00 max)
      #  -HH:MM (-12:00 min)
      #
      # This function can process roughly 10K timestamps/sec.
      # By comparison, the popular-and-otherwise-correct lubridate library is 60x slower at ~ 150 timestamps per second
      len <- nchar(isoText)

      if (substr(isoText, len - 2, len - 2) == ":") {
        # The most common scenario from AQTS output: A truly correct ISO 8601 timestamp with a numeric UTC offset
        # Strip out the colon separating the UTC offset, since that is what %z requires
        isoText <- paste0(substr(isoText, 1, len - 3), substr(isoText, len - 1, len))
      } else if (substr(isoText, len, len) == "Z") {
        # Second most likely scenario from AQTS output: The "Z" representing a UTC time
        # Convert the unsupported UTC shorthand into an offset with no effect
        isoText <- paste0(substr(isoText, 1, len - 1), "+0000")
      }

      as.POSIXct(strptime(isoText, "%Y-%m-%dT%H:%M:%OS%z", "UTC"))
    },

#' Formats a datetime in ISO 8601 format
#' 
#' @param datetime A datetime object
#' @return The time in YYYY-MM-DDTHH:mm:SS.fffffZ format
    formatIso8601 = function(datetime) {
      isoText <- strftime(datetime, "%Y-%m-%dT%H:%M:%OS%z", "UTC")
      
      len <- nchar(isoText)
      
      if (substr(isoText, len, len) != "Z") {
        # Inject the missing colon in the zone offset, so "+HHMM" becomes "+HH:MM"
        isoText = paste0(substr(isoText, 1, len - 2), ":", substr(isoText, len - 1, len))
      }
      
      isoText
    },

#' Gets the UTC offset string from a numeric UTC offset hours value
#'
#' @param utcOffset A UTC offset in hours
#' @return An ISO8601 UTC offset string in +/-HH:MM format
#' @examples
#' getUtcOffsetText(-8) # "-08:00"
#' getUtcOffsetText(2.5) # "+02:30"
    getUtcOffsetText = function(utcOffset) {
      isNegative <- FALSE
      totalMinutes <- as.integer(utcOffset * 60)

      if (totalMinutes < 0) {
        isNegative = TRUE
        totalMinutes = -totalMinutes
      }

      hours = as.integer(totalMinutes / 60)
      minutes = as.integer(totalMinutes %% 60)

      sprintf("%s%02d:%02d", if (isNegative) "-" else "+", hours, minutes)
    },

#' Gets the location data for a location
    getLocationData = function(locationIdentifier) {
      locationData <- fromJSON(content(stop_for_status(
        GET(paste0(publishUri, "/GetLocationData"), query = list(LocationIdentifier = locationIdentifier))
      , paste("get location data for", locationIdentifier)), "text"))
    },

#' Gets the rating models matching the optional filters
#' 
#' Retrieves the rating models and their applicable curves
#' 
#' @param locationIdentifier Optional LocationIdentifier filter
#' @param queryFrom Optional QueryFrom filter
#' @param queryTo Optional QueryTo filter
#' @param inputParameter Optional InputParameter filter
#' @param outputParameter Optional OutputParameter filter
#' @return A list of rating models and their applicable curves
#' @examples 
#' # Get all the ratings in effect during October 2016 at a location
#' ratings = timeseries$getRatings("A015001", "2016-10-01", "2016-10-31")
#' ratings$Identifier
#' ratings$Curves$Type
  getRatings = function(locationIdentifier, queryFrom, queryTo, inputParameter, outputParameter) {
    
    if (missing(locationIdentifier))  { locationIdentifier = NULL }
    if (missing(inputParameter))      { inputParameter = NULL }
    if (missing(outputParameter))     { outputParameter = NULL }
    if (missing(queryFrom))           { queryFrom = NULL }
    if (missing(queryTo))             { queryTo = NULL }
    
    # Coerce native R dates to an ISO 8601 string
    if (is.double(queryFrom)) { queryFrom <- .self$formatIso8601(queryFrom) }
    if (is.double(queryTo))   { queryTo   <- .self$formatIso8601(queryTo) }

    # Build the rating model query
    q <- list(
      LocationIdentifier = locationIdentifier,
      InputParameter = inputParameter,
      OutputParameter = outputParameter,
      QueryFrom = queryFrom,
      QueryTo = queryTo)
    q <- q[!sapply(q, is.null)]

    # Get the rating models for the time period
    ratingModels <- fromJSON(content(stop_for_status(
      GET(paste0(.self$publishUri, "/GetRatingModelDescriptionList"), query = q)
    , paste("get rating models for", locationIdentifier)), "text"))$RatingModelDescriptions
    
    # Get the rating curves active in those models
    ratingCurveRequests <- lapply(ratingModels$Identifier, function(identifier) {
      r <- list(
        RatingModelIdentifier = identifier,
        QueryFrom = queryFrom,
        QueryTo = queryTo)
      r <- r[!sapply(r, is.null)]
      })
    
    ratingCurves <- .self$sendBatchRequests(.self$publishUri, "RatingCurveListServiceRequest", "/GetRatingCurveList", ratingCurveRequests)
    ratingModels$Curves <- ratingCurves$RatingCurves
    
    ratingModels
  },

#' Gets output values from a rating model
#' 
#' @param ratingModelIdentifier The identifier of the rating model
#' @param inputValues The list of input values to run through the model
#' @param effectiveTime Optional time of applicability. Assumes current time if omitted
#' @param applyShifts Optional boolean, defaults to FALSE
#' @return The output values from the applicable curve of the rating model. An output value of NA is returned if the input is outside the curve.
  getRatingModelOutputValues = function(ratingModelIdentifier, inputValues, effectiveTime, applyShifts) {
    
    if (missing(effectiveTime)) { effectiveTime <- NULL }
    if (missing(applyShifts))   { applyShifts <- NULL }
    
    # Coerce native R dates to an ISO 8601 string
    if (is.double(effectiveTime)) { effectiveTime <- .self$formatIso8601(effectiveTime) }
    
    # Build the query
    q <- list(
      RatingModelIdentifier = ratingModelIdentifier,
      InputValues = .self$toJSV(inputValues),
      EffectiveTime = effectiveTime,
      ApplyShifts = applyShifts
    )
    q <- q[!sapply(q, is.null)]
    
    # Get the output values of the rating curve
    outputValues <- fromJSON(content(stop_for_status(
      GET(paste0(.self$publishUri, "/GetRatingModelOutputValues"), query = q)
    , paste("get rating model output values for", ratingModelIdentifier)), "text"))$OutputValues
    
  },

#' Gets field visits
#' 
#' Gets field visits activities
#' 
#' @param locationIdentifier Optional LocationIdentifier filter
#' @param queryFrom Optional QueryFrom filter
#' @param queryTo Optional QueryTo filter
#' @param activityType Optional DiscreteMeasurementActivity filter
#' @return The activities performed at the locations during the requested time range
#' @examples 
  getFieldVisits = function(locationIdentifier, queryFrom, queryTo, activityType) {
    
    if (missing(locationIdentifier))  { locationIdentifier = NULL }
    if (missing(queryFrom))           { queryFrom = NULL }
    if (missing(queryTo))             { queryTo = NULL }
    if (missing(activityType))        { activityType = NULL }
    
    # Coerce native R dates to an ISO 8601 string
    if (is.double(queryFrom)) { queryFrom <- .self$formatIso8601(queryFrom) }
    if (is.double(queryTo))   { queryTo   <- .self$formatIso8601(queryTo) }
    
    # Build the field visit query
    q <- list(
      LocationIdentifier = locationIdentifier,
      QueryFrom = queryFrom,
      QueryTo = queryTo)
    q <- q[!sapply(q, is.null)]
    
    # Get the filed visits descriptions for the time period
    visits <- fromJSON(content(stop_for_status(
      GET(paste0(.self$publishUri, "/GetFieldVisitDescriptionList"), query = q)
      , paste("get field visits for", locationIdentifier)), "text"))$FieldVisitDescriptions
    
    # Get the activities performed during those visits
    visitDataRequests <- lapply(visits$Identifier, function(identifier) {
      r <- list(
        FieldVisitIdentifier = identifier,
        DiscreteMeasurementActivity = activityType)
      r <- r[!sapply(r, is.null)]
    })
    
    visitData <- .self$sendBatchRequests(.self$publishUri, "FieldVisitDataServiceRequest", "/GetFieldVisitData", visitDataRequests)
    visits$Details <- visitData
    
    visits
  },

  #' Gets Change list for a given time-series
  #' 
  #' @param timeSeriesIdentifier The time-series identifier or unique ID
  #' @param queryFrom Optional QueryFrom filter
  #' @param queryTo Optional QueryTo filter
  #' @return The list of change trasactions
  #' @examples 
  getMetadataChangeTransactionList = function(timeSeriesIdentifier, queryFrom, queryTo) {
    
    if (missing(queryFrom))     { queryFrom <- NULL }
    if (missing(queryTo))       { queryTo <- NULL }
    
    # Coerce native R dates to an ISO 8601 string
    if (is.double(queryFrom)) { queryFrom <- timeseries$formatIso8601(queryFrom) }
    if (is.double(queryTo))   { queryTo   <- timeseries$formatIso8601(queryTo) }
    
    # Build the query
    q <- list(
      TimeSeriesUniqueId = .self$getTimeSeriesUniqueId(timeSeriesIdentifier),
      QueryFrom = queryFrom,
      QueryTo = queryTo
    )
    q <- q[!sapply(q, is.null)]
    
    # Get metadata transaction list
    metadataChangeList <- fromJSON(content(stop_for_status(
      GET(paste0(.self$publishUri, "/GetMetadataChangeTransactionList"), query = q)
      , paste("get metadata change list for", timeSeriesIdentifier)), "text"))
    
    metadataChangeList
  },

#' Converts an item to JSV format, for GET request query parameter values
#' 
#' Converts vectors or named lists to JSV. Everything else is left unmodified.
#' 
#' Query parameters in a GET request need to be in JSV format.
#' JSON body parameters in POST/PUT/DELETE requests do not need JSV formatting (they are, JSON)
#' 
#' https://github.com/ServiceStack/ServiceStack.Text/wiki/JSV-Format
#' 
  toJSV = function(item) {
    if (is.list(item)) {
      if (is.null(names(item))) {
        
        # Treat lists without names like a vector
        item = .self$toJSV(unlist(item))
      } else {
        
        # List the key value pairs
        n = names(item)
        v = unlist(item)
        item <- .self$toJSV(sapply(seq_along(item), function (i) paste0(n[i], ":", v[i])))
      }
    } else if (is.vector(item)) {
      
      # Commas separate arrays
      item <- paste0(item, collapse = ",")
    }
    
    item
  },

#' Fetch all requested time-series descriptions matching the filter
#' 
#' @param locationIdentifier Optional location identifier filter
#' @param parameter Optional parameter filter
#' @param publish Optional publish filter
#' @param computationIdentifier Optional computation identifier filter
#' @param computationPeriodIdentifier Optional computation period identifier filter
#' @param extendedFilters Optional extended attribute filter
#' @return All the time-series descriptions matching the filters
  getTimeSeriesDescriptions = function(locationIdentifier, parameter, publish, computationIdentifier, computationPeriodIdentifier, extendedFilters) {
    
    if (missing(locationIdentifier))          { locationIdentifier = NULL }
    if (missing(parameter))                   { parameter = NULL }
    if (missing(publish))                     { publish = NULL }
    if (missing(computationIdentifier))       { computationIdentifier = NULL }
    if (missing(computationPeriodIdentifier)) { computationPeriodIdentifier = NULL }
    if (missing(extendedFilters))             { extendedFilters = NULL }
    
    q <- list(
      LocationIdentifier = locationIdentifier,
      Parameter = parameter,
      Publish = publish,
      ComputationIdentifier = computationIdentifier,
      ExtendedFilters = .self$toJSV(extendedFilters))
    q <- q[!sapply(q, is.null)]
    
    # Get all the time series at the location
    timeSeries <- fromJSON(content(stop_for_status(
      GET(paste0(timeseries$publishUri, "/GetTimeSeriesDescriptionList"), query = q)
      , paste("get time-series descriptions for", locationIdentifier)), "text"))$TimeSeriesDescriptions
  },

#' Gets time-series points for multiple time-series
#' 
#' Retrieves points for up to 10 time-series.
#' Point values from secondary time-series will be time-aligned via interpolation
#' rules to the timestamps from the first time-series.
#' 
#' @param timeSeriesIds A list of time-series identifiers or unique IDs
#' @param queryFrom Optional time from which to retrieve data.If missing, fetches data from the start-of-record
#' @param queryTo Optional time to which data willl be retrieved. If missing, fetches data to the end-of-record
#' @param outputUnitIds Optional unit IDs for output. If missing or empty, the default unit of the time-series will be used
#' @returns The JSON object from the /GetTimeSeriesData response
#' @examples
#' ## Get the discharge and stage timeseries for 2012
#' json = timeseries$getTimeSeriesData(c("Discharge.Working@Location","Stage.Working@Location"),
#'                                     queryFrom = "2012-01-01T00:00:00Z",
#'                                     queryTo   = "2013-01-01T00:00:00Z")
#'
#' ## Plot stage vs dicharge
#' plot(json$Points$NumericValue1, json$Points$NumericValue2)
#'
#' ## Plot stage vs dicharge, with log scale, and some labeled axis
#' plot(json$Points$NumericValue1, json$Points$NumericValue2, log = "xy",
#'      xlab = json$TimeSeries$Identifier[1],
#'      ylab = json$TimeSeries$Identifier[2])
    getTimeSeriesData = function(timeSeriesIds, queryFrom, queryTo, outputUnitIds, includeGapMarkers) {
      if (.self$isVersionLessThan("17.2")) {
        # Throw on the brakes if the server is too old
        stop("Time aligned data is not available before AQTS 2017.2. Connected server version=", version)
      }

      if (is.character(timeSeriesIds)) {
        # Coerce a single timeseries ID string into a vector
        timeSeriesIds <- c(timeSeriesIds)
      }

      uniqueIds <- lapply(timeSeriesIds, .self$getTimeSeriesUniqueId)
      
      if (missing(queryFrom))     { queryFrom <- NULL }
      if (missing(queryTo))       { queryTo <- NULL }
      if (missing(outputUnitIds)) { outputUnitIds <- NULL }
      if (missing(includeGapMarkers)) { includeGapMarkers <- NULL }
      
      # Coerce native R dates to an ISO 8601 string
      if (is.double(queryFrom)) { queryFrom <- .self$formatIso8601(queryFrom) }
      if (is.double(queryTo))   { queryTo   <- .self$formatIso8601(queryTo) }

      q <- list(
        TimeSeriesUniqueIds = .self$toJSV(uniqueIds),
        TimeSeriesOutputUnitIds = .self$toJSV(outputUnitIds),
        QueryFrom = queryFrom,
        QueryTo = queryTo,
        IncludeGapMarkers = includeGapMarkers)
      q <- q[!sapply(q, is.null)]
      
      r <- GET(paste0(publishUri, "/GetTimeSeriesData"), query = q)
      stop_for_status(r, paste("get time-aligned data for", length(uniqueIds), "time-series"))

      j <- fromJSON(content(r, "text"))
    },

#' Get corrected data for a time-series
#'
#' The getTimeSeriesData() method is usually a better choice, since it can pull corrected data from multiple time-series.
#' But when you need to look at the metadata of a time-series, this method is required.
#' 
#' @param timeSeriesIdentifier
#' @return The corrected data and metadata for the time-series
  getTimeSeriesCorrectedData = function (timeSeriesIdentifier, queryFrom, queryTo, getParts, includeGapMarkers) {
    
    if (missing(queryFrom))         { queryFrom <- NULL }
    if (missing(queryTo))           { queryTo <- NULL }
    if (missing(getParts))          { getParts <- NULL }
    if (missing(includeGapMarkers)) { includeGapMarkers <- NULL }
    
    # Coerce native R dates to an ISO 8601 string
    if (is.double(queryFrom)) { queryFrom <- .self$formatIso8601(queryFrom) }
    if (is.double(queryTo))   { queryTo   <- .self$formatIso8601(queryTo) }
    
    # Build the query
    if (isLegacy) {
      q <- list(
        TimeSeriesIdentifier = timeSeriesIdentifier,
        QueryFrom = queryFrom,
        QueryTo = queryTo,
        GetParts = getParts,
        IncludeGapMarkers = includeGapMarkers)
    } else {
      q <- list(
        TimeSeriesUniqueId = .self$getTimeSeriesUniqueId(timeSeriesIdentifier),
        QueryFrom = queryFrom,
        QueryTo = queryTo,
        GetParts = getParts,
        IncludeGapMarkers = includeGapMarkers)
    }
    q <- q[!sapply(q, is.null)]
    
    data <- fromJSON(content(stop_for_status(
      GET(paste0(.self$publishUri, "/GetTimeSeriesCorrectedData"), query = q)
    , paste("get corrected data for", timeSeriesIdentifier)), "text"))
    
  },

#' Uploads a file to a location as an external report
#'
#' Any existing report on AQTS with the same title will be 
#'
#' @param locationDataOrIdentifier Either a location identifier string, or a LocationData object from a previous getLocationData request
#' @param path The path to the file to be uploaded
#' @param title The title of the report to display in AQTS
#' @param deleteDuplicateReports If TRUE or missing, any existing reports with the same title will be deleted before the new report is uploaded
#' @returns The JSON response from a successful upload
  uploadExternalReport = function(locationDataOrIdentifier, path, title, deleteDuplicateReports) {
    if (.self$isVersionLessThan("17.3")) {
      # Throw on the brakes if the server is too old
      stop("Uploading external reports is not available before AQTS 2017.3. Connected server version=", version)
    }
  
    if (missing(title)) {
      stop("A report title is required.")
    }
  
    if (!file.exists(path)) {
      stop("Can't upload non-existent file '", path, "'")
    }
  
    if (missing(deleteDuplicateReports)) {
      # Delete duplicate reports by default
      deleteDuplicateReports = TRUE
    }
  
    locationData <- locationDataOrIdentifier
  
    if (!is.list(locationData) || is.null(locationData$UniqueId)) {
      # We don't know the location unique ID, so look it up from the identifier string
      locationData <- .self$getLocationData(locationDataOrIdentifier)
    }
  
    if (deleteDuplicateReports) {
  
      reports = .self$getReportList()$Reports
  
      if (length(reports)) {
        for (row in 1:nrow(reports)) {
  
          if (reports[row, "IsTransient"]) {
            # Ignore transient reports
            next
          }
    
          if (reports[row, "Title"] != title) {
            # Ignore permanent reports whose title does not match
            next
          }
    
          .self$deleteReport(reports[row, "ReportUniqueId"])
        }
      }
    }
  
    # Upload the file to the location
    r <- POST(paste0(acquisitionUri, "/locations/", locationData$UniqueId, "/attachments/reports"),
       body = list(uploadedFile = upload_file(path), Title = title))
    stop_for_status(r, paste("upload external report to location", locationData$Identifier))
  
    j <- fromJSON(content(r, "text"))
  
  },

#' Gets a list of reports
  getReportList = function() {
  
    r <- GET(paste0(publishUri, "/GetReportList"))
    stop_for_status(r, "get report list")
  
    j <- fromJSON(content(r, "text"))
  },

#' Deletes a report
  deleteReport = function(reportUniqueId) {
    r <- DELETE(paste0(acquisitionUri, "/attachments/reports/", reportUniqueId))
    stop_for_status(r, paste("delete report", reportUniqueId))
  },

#' Performs a batch of identical operations
#' 
#' This method is useful for requesting large amounts of similar data from AQTS,
#' taking advantage of ServiceStack's support for auto-batched requests.
#' 
#' http://docs.servicestack.net/auto-batched-requests
#' 
#' When you find that a public API only supports a 1-at-a-time approach, and your
#' code needs to request thousands of items, the sendBatchRequests() method is the one to use.
#' 
#' @param endpoint The base REST endpoint
#' @param operationName The name of operation, from the AQTS Metadata page, to perform multiple times. NOT the route, but the operation name.
#' @param operationRoute The route of the operation
#' @param requests A collection of individual request objects
#' @param batchSize Optional batch size (defaults to 100 requests per batch)
#' @param verb Optional HTTP verb of the operation (defaults to "GET")
#' @returns A single dataframe containing all the batched responses
#' @examples 
#' # Request info for 3 locations.
#' # Single-request URL is GET /Publish/v2/GetLocationData?LocationIdentifer=loc1
#' # Operation name is "LocationDataServiceRequest"
#' requests = c(list(LocationIdentifier="Loc1"), list(LocationIdentifier="Loc3"), list(LocationIdentifier="Loc3"))
#' responses = timeseries$sendBatchRequests(timeseries$publishUri,"LocationDataServiceRequest", requests)
  sendBatchRequests = function(endpoint, operationName, operationRoute, requests, batchSize, verb) {
    
    if (missing(batchSize)) { batchSize <- 100 }
    if (missing(verb))      { verb <- "GET" }
    
    if (batchSize > 500)    { batchSize <- 500 }
    
    if (isLegacy) {
      # No batch support in 3.X, so just perform each request sequentially
      lapply(requests, function(request) {
        # TODO: Support more that GET requests
        fromJSON(content(stop_for_status(
          GET(paste0(.self$publishUri, operationRoute), query = request)
          , paste(operationRoute)), "text"))
      })
      
    } else {
    
      # Compose the special batch-operation URL supported by ServiceStack
      url = paste0(endpoint, "/json/reply/", operationName, "[]")
      
      # Split the requests into batch-sized chunks
      requestBatches <- split(requests, ceiling(seq_along(requests) / batchSize))
  
      # Create a local function to request each batch of requests, using the verb as an override to the POST
      batchPost <- function(batchOfRequests, index) {
        offset <- batchSize * index
        r <- POST(url, body = batchOfRequests, encode = "json", add_headers("X-Http-Method-Override" = verb))
        stop_for_status(r, paste("receive", length(batchOfRequests), "batch", operationRoute, "responses at offset", offset))
        
        # Return the batch of responses
        responses <- fromJSON(content(r, "text"))
      }
      
      # Call the operation in batches
      responseBatches <- mapply(batchPost, requestBatches, seq_along(requestBatches) - 1, SIMPLIFY = FALSE)
      
      # Flatten the list of response data frames into a single data frame
      rbind_pages(responseBatches)
    }
  }
  )
)

# Create a client in the global namespace
timeseries = timeseriesClient()
