library(rapiclient)
library(httr)
library(jsonlite)
library(dplyr)

#' Stops when an HTTP error is received
#' 
#' Returns successful HTTP responses, but stops if an error occurs
#' 
#' @param response An httr response object
#' @param ... Optional parameters for the httr::content() function
#' @return the httr response object
content_or_stop <- function(response, ...) {
  res <- httr::stop_for_status(response)
  if(inherits(res, "response")) {
    httr::content(res, ...)
  } else {
    res
  }
}

#' Returns a data frame of the JSON response
#' 
#' Returns a data frame of the JSON response, but stops if an error occurs
#' 
#' @param response An httr response object
#' @return A data frame of the JSON response
json_or_stop <- function(response) {
     fromJSON(content_or_stop(response, as = "text", encoding = "UTF-8"))
}

#' Gets an authenticated samples API client
#' 
#' Builds an a dynamic, authenticated client for your samples instance
#' 
#' @param url Base URL of your samples instance. "https://{yourinstance}.aqsamples.com"
#' @param api_token Browse to "https://{yourinstance}.aqsamples.com/api" to retrieve your account's api_token
#' @return An authenticated client for your samples instance
#' @examples 
#' # Create an authenticated client and retrieve names of the labs
#' samples <- get_samples_client("https://ai.aqsamples.com.au", "01234567890123456789012345678901")
#' labs <- samples$getLaboratories()
#' labs$domainObjects$name
#' [1] "Citilab"              "ELS"                  "Endetec QLDC"         "Eurofins"             "Eurofins ELS Limited"
#' [6] "Hill Laboratories"    "Unknown laboratory"   "WaterCare"            "Water Care"   
get_samples_client <- function(url, api_token) {
  get_operations(get_api(paste0(url, "/api/swagger.json")), .headers = c("Authorization" = paste0("token ", api_token)), handle_response = json_or_stop)
}

#' Requests all pages of a paginated API operation
#' 
#' Many SAMPLES API operations are paginated, responding with a single page of data,
#' plus a cursor to request with the next page.
#' 
#' This function will make multiple operation requests, until all pages have been received.
#' 
#' WARNING: This can sometimes take many days to complete, so please add more filters to the operation to
#' reduce the response payload.
#' 
#' @param operation The operation function
#' @param ... Other filter arguments for the operation
#' @return A single data frame containing all the responses' domain objects
#' @examples 
#' # Get sampling locations with "hill" in their name
#' samples <- get_samples_client("https://ai.aqsamples.com.au", "01234567890123456789012345678901")
#' hill_locations <- paginated_get(samples$getSamplingLocations, search = "hill")
#' 100 of 258 items received.
#' 200 of 258 items received.
paginated_get <- function(operation, ...) {
  response <- operation(...)
  all_objects <- response$domainObjects
  while (nrow(all_objects) < response$totalCount ) {
    message(nrow(all_objects), " of ", response$totalCount, " items received.")
    response = operation(cursor = response$cursor, ...)
    if (nrow(response$domainObjects) < 1) {
      message("No more items received")
      break
    }
    all_objects <- bind_rows(all_objects, response$domainObjects)#c(all_objects, response$domainObjects)
  }
  all_objects
}
