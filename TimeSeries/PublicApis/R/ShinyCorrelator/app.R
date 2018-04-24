
library(shiny)

# Configuration values for this script
config = list(
  server = "aq-demo.aquaticinformatics.com", username = "admin", password = "admin",     # AQTS credentials for your server
  timeSeries1Name = "Discharge.Telemetry@03353200",                   # The 1st time-series to analyze
  timeSeries2Name = "Stage.Telemetry@03353200",                  # The 2nd time-series to analyze
  eventPeriodStartDay = "2018-01-01", eventPeriodEndDay = "2018-12-31") # The period to analyze

# Define UI for app
ui <- fluidPage(
  
  # App title ----
  titlePanel("Correlation Test"),
  
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
      textInput(inputId = "timeSeries1Name",
                label = "1st time-series:",
                value = config$timeSeries1Name),   
      textInput(inputId = "timeSeries2Name",
                label = "2nd AQ time-series:",
                value = config$timeSeries2Name),
      selectInput(inputId = "method",
                  label = "Correlation method:",
                  choices = c("pearson","kendall","spearman")),
      dateRangeInput("daterange", "Time-series Range:",
                     start  = config$eventPeriodStartDay,
                     end    = config$eventPeriodEndDay),
      submitButton("Update View", icon("refresh"))

    ),
    
    # Main panel for displaying outputs ----
    mainPanel(
      textOutput(outputId = "status"),
      plotOutput(outputId = "distPlot")
    )
  )
)



# Define server logic
server <- function(input, output) {
  tryCatch({
    
    # Generate a new plot when the "Update View" button is clicked
    doServer(input, output)
  },
  warning = function(w) { output$status <- renderText(paste("WARNING:", w)) },
  error = function(e) { output$status <- renderText(paste("ERROR:", e)) })
}

doServer <- function(input, output) {

  library(jsonlite)
  library(httr)
  library(ggpubr)

  # Load supporting code
  source("./timeseries_client.R")

  output$distPlot <- renderPlot({
    
    withProgress(message = paste0("Connecting to ", config$server, " ..."), value = 0.1,{
      
      tryCatch({
        # Connect to the AQTS server
        timeseries$connect(input$server, input$username, input$password)
        
        # Get the location data
        locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(input$timeSeries1Name))
        utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)
        
        startOfDay = "T00:00:00"
        endOfDay = "T23:59:59.9999999"
        
        # event period
        fromperiodStart = paste0(input$daterange[1], startOfDay, utcOffset)
        toperiodEnd = paste0(input$daterange[2], endOfDay, utcOffset)
        periodLabel = sprintf("%s - %s", input$daterange[1], input$daterange[1])
        
        #read time-series from AQ API
        json <- timeseries$getTimeSeriesData(c(input$timeSeries1Name, input$timeSeries2Name),
                                             queryFrom = fromperiodStart,
                                             queryTo = toperiodEnd)    
        if (json$NumPoints > 0) {
          
          ggscatter(json$Points,
                    x = "NumericValue1", xlab = json$TimeSeries$Identifier[1],
                    y = "NumericValue2", ylab = json$TimeSeries$Identifier[2],
                    add = "reg.line", conf.int = TRUE, 
                    cor.coef = TRUE, cor.method = input$method)
          
        } else {
          # No data
          #print(c("No data found during:",periodLabel))
        }
      },
      warning = function(w) { warning(w) },
      error = function(e) { stop(e) },
      finally = {
        tryCatch({
          timeseries$disconnect() # Always try to disconnect from AQTS
        },
        warning = function(w){},
        error = function(e){})
      })
    })
    
  },height = 600)
  
}


shinyApp(ui = ui, server = server)
