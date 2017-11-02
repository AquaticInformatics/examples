

library(shiny)


rm(list = ls())   

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Discharge.Working@Reporting01",                     # The time-series to analyze
  eventPeriodStartDay = "2011-01-01", eventPeriodEndDay = "2011-12-31") # The period to analyze

# Define UI for app that draws a histogram ----
ui <- fluidPage(
  
  # App title ----
  titlePanel("Flow Duration Curve Example"),
  
  # Sidebar layout with input and output definitions ----
  sidebarLayout(
    
    # Sidebar panel for inputs ----
    sidebarPanel(
      
      # Input text
      textInput(inputId = "server",
                  label = "AQTS Server:",
                  value = config$server),
      textInput(inputId = "username",
                label = "User:",
                value = config$username),
      passwordInput(inputId = "password",
                label = "Password:",
                value = config$password),     
      textInput(inputId = "timeSeriesName",
                label = "AQ time-series Name:",
                value = config$timeSeriesName),   
      dateRangeInput("daterange", "Time-series Range:",
                     start  = config$eventPeriodStartDay,
                     end    = config$eventPeriodEndDay),
      submitButton("Update View", icon("refresh"))

    ),
    
    # Main panel for displaying outputs ----
    mainPanel(
      # Output: plot
      plotOutput(outputId = "distPlot")
      
    )
  )
)



# Define server logic required to draw a histogram ----
server <- function(input, output) {

  require('jsonlite')
  require('httr')
  require(hydroTSM)
  require(mgcv)  
  # Load supporting code
  source("./timeseries_client.R")

  output$distPlot <- renderPlot({
    
    # Connect to the AQTS server
    timeseries$connect(input$server, input$username, input$password)
    
    # Get the location data
    locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(input$timeSeriesName))
    utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)
    
    startOfDay = "T00:00:00"
    endOfDay = "T23:59:59.9999999"
    
    # event period
    fromperiodStart = paste0(input$daterange[1], startOfDay, utcOffset)
    toperiodEnd = paste0(input$daterange[2], endOfDay, utcOffset)
    periodLabel = sprintf("%s - %s", input$daterange[1], input$daterange[1])
    
    #read time-series from AQ API
    json <- timeseries$getTimeSeriesData(c(input$timeSeriesName),
                                         queryFrom = fromperiodStart,
                                         queryTo = toperiodEnd)    
    if (json$NumPoints > 0) {
      # convert AQTS timestamps to POSIXct values
      timeStamp2 = strptime(substr(json$Points$Timestamp,0,19), "%FT%T")
      TS = zoo(json$Points$NumericValue1, timeStamp2)
      
      #calculate daily from original frequency 
      TSD = subdaily2daily(TS, FUN=mean, na.rm = TRUE)
      
      
      par(mfrow=c(2,1))
      
      #override tick labels for y axis
      ytick = c(1,2,5,10,20,30,40,50,70,100,150,200)
      
      #call fdc from hydroTSM package to plot FDC
      #windows()
      fdcPlotTitle = paste("Flow-Duration curve for", locationData$LocationName, "Site during:\n", periodLabel)
      fdc(TSD, main = fdcPlotTitle, xlab = "Percentage Exceedance", ylab = sprintf("Flow (%s)", json$TimeSeries$Unit[1]) , yat = ytick, panel.first = grid(), col="blue")
      
      #plot daily flow hydrograph
      #windows()
      flowPlotTitle = paste("Daily Flow for", locationData$LocationName, "Site during:\n", periodLabel)
      plot(TSD, main = flowPlotTitle , xlab = "Time", ylab = sprintf("Flow (%s)", json$TimeSeries$Unit[1]),col="blue")
      grid()
      
            
      
    } else {
      # No data
      #print(c("No data found during:",periodLabel))
    }
    
    
    
    #x    <- faithful$waiting
    #plot(x, main = input$daterange[1])
     
  },height = 600)
  
}



shinyApp(ui = ui, server = server)


#runApp(appDir = getwd(), port = getOption("shiny.port"),
#       +   launch.browser = getOption("shiny.launch.browser", interactive()),
#       +   host = getOption("shiny.host", "0.0.0.0"), workerId = "",
#       +   quiet = FALSE, display.mode = c("auto", "normal", "showcase"),
#       +   test.mode = getOption("shiny.testmode", FALSE))



