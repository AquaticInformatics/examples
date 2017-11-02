
library(shiny)
library(plotly)
library(DT)

# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Discharge.Working@Reporting01", # The time-series to analyze
  eventPeriodStartDay = "2002-01-01", eventPeriodEndDay = "2015-12-31") # The period to analyze

# Define UI for random distribution app ----
ui <- fluidPage(
  
  # App title ----
  titlePanel("Extreme Value Analysis (EVA)"),
  
  # Sidebar layout with input and output definitions ----
  sidebarLayout(
    
    # Sidebar panel for inputs ----
    sidebarPanel(
      
      # Input: Select the distribution type ----
      selectInput(inputId = "dist",
                  label = "Distribution type:",
                  choices = c("Generalized extreme value (GEV)" = "GEV",
                              "Gumbel" = "Gumbel",
                              "Exponential" = "Exponential"),
                  selected = "Gumbel"),
      
      
      # Input: Select estimation method ----
      selectInput(inputId = "method",
                  label = "Estimation Methods:",
                  choices = c("MLE" = "MLE",
                              "GMLE" = "GMLE"),
                  selected = "MLE"),
      
      # br() element to introduce extra vertical spacing ----
      br(),
      
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
      
      # Output: Tabset w/ plot, summary, and table ----
      tabsetPanel(type = "tabs",
                  tabPanel("Extreme Values",fluidRow(column(12,br()), column(12,plotlyOutput('extremePlot', height = 500))
                                                     , column(12,br()),column(4,DT::dataTableOutput('extremeTable')))),
                  tabPanel("Validation Plots", plotOutput("valPlot")),
                  tabPanel("Return Period Curve", downloadButton('downloadRP'), plotOutput("returnPlot"))
      )
    )
  )
)



# Define server logic for random distribution app ----
server <- function(input, output) {
  library(extRemes)
  require('jsonlite')
  require('httr')
  require(hydroTSM)
  require(mgcv)
  
  # Load supporting code
  source("./timeseries_client.R")
  
  
  # Reactive expression to generate the requested distribution ----
  # This is called whenever the inputs change. The output functions
  # defined below then use the value computed from this expression
  d <- reactive({
    
    # Connect to the AQTS server
    timeseries$connect(config$server, config$username, config$password)
    
    
    # Get the location data
    locationData = timeseries$getLocationData(timeseries$getLocationIdentifier(input$timeSeriesName))
    utcOffset = timeseries$getUtcOffsetText(locationData$UtcOffset)
    
    startOfDay = "T00:00:00"
    endOfDay = "T23:59:59.9999999"
    
    # event period
    fromperiodStart = paste0(input$daterange[1], startOfDay, utcOffset)
    toperiodEnd = paste0(input$daterange[2], endOfDay, utcOffset)

    
    showNotification(id = "wait", "Reading and processing data from AQUARIUS. Please Wait...", duration = NULL, type = "message")
    #read time-series from AQ API
    json <- timeseries$getTimeSeriesData(c(input$timeSeriesName),
                                         queryFrom = fromperiodStart,
                                         queryTo = toperiodEnd)
    
    periodLabel = sprintf("%s - %s", input$daterange[1], input$daterange[2])
    unit = json$TimeSeries$Unit[1]
    titlem = paste("Annual peaks for", locationData$LocationName, "Site during:\n", periodLabel)

    
    if (json$NumPoints > 0) {
      # convert AQTS timestamps to POSIXct values
      timeStamp2 = strptime(substr(json$Points$Timestamp,0,19), "%FT%T")
      TS = zoo(json$Points$NumericValue1, timeStamp2)
      
      #calculate annual max from original frequency and then annual
      # I don't know why subdaily2annual doesn't work
      TSD1 = subdaily2daily(TS, FUN=max, na.rm = TRUE)
      TSD2 = daily2annual(TSD1, FUN=max, na.rm = TRUE)
      tm = format(as.Date(index(TSD2), format="%d/%m/%Y"),"%Y")
      dat = data.frame(tm, coredata(TSD2), unit, titlem )
      names(dat) = c("x","y","unit","titlem")

      dat
      removeNotification(id = "wait")

    } else {
      # No data
      dat = NA
    }
    
    dat
    
  })
  
  fitm <- reactive({
    fito=fevd(y, data = d(), type = input$dist , method = input$method ,threshold = 0.001)
  })

  # Generate a plot of the data ----
  # Also uses the inputs to build the plot label. Note that the
  # dependencies on the inputs and the data reactive expression are
  # both tracked, and all expressions are called in the sequence
  # implied by the dependency graph.
  output$extremePlot <- renderPlotly({
    dist <- input$dist
    n <- 20
    #s = input$x1_rows_selected
    p = plot_ly(d(),x=~x,y=~y, type = 'scatter', mode = 'lines')
    p = layout(p, title = ~titlem , xaxis=list(title=""), yaxis=list(title=~unit), showlegend = FALSE)
    add_trace(p, d(),x=~x,y=~y, type = 'scatter', mode = 'markers')
    
  })
  
  # Generate validation plots ----
  output$valPlot <- renderPlot({
    #data(Fort)
    #bmFort <- blockmaxxer(Fort, blocks = Fort$year, which="Prec")
    #fitGEV <- fevd(y, data = d())
    par(mfrow=c(2,2))
    plot(fitm(), type="prob", main = NA)
    plot(fitm(), type="qq", main = NA)
    #plot(fitm(), type="qq2", main = NA)
    plot(fitm(), type="density", main = NA)
    plot(fitm(), type="rl", main = NA)
    title(main="\n\nDensity Estimation Validation Curves",outer=T)
  }, height = 800)
  
  # Generate an HTML table view of the data ----
  output$extremeTable <- DT::renderDataTable({
    dtf = data.frame(d()$x, d()$y)
    names(dtf) = c("time","values")
    
    DT::datatable(dtf)
  })
  
  # Generate validation plots ----
  output$returnPlot <- renderPlot({
    #fitGEV <- fevd(y, data = d())
    plot(fitm(), type="rl" , main="Return Period Curve")
    grid()
  }, height = 700)
  
  output$downloadRP = downloadHandler(
    filename = "returncurve.pdf",
    content = function(file) {
      pdf(file = file)
      plot(fitm(), type="rl", main="Return Period Curve")
      grid()
      dev.off()
    })
}

# Create Shiny app ----
shinyApp(ui, server)



# to run and access from the network run this from the command line
#runApp(appDir = getwd(), port = getOption("shiny.port"),
#       launch.browser = getOption("shiny.launch.browser", interactive()),
#       host = getOption("shiny.host", "0.0.0.0"), workerId = "",
#       quiet = FALSE, display.mode = c("auto", "normal", "showcase"),
#       test.mode = getOption("shiny.testmode", FALSE))

