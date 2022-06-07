## Consuming AQUARIUS Time-Series data from R

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FR)

The R programming environment provides a rich ecosystem for numerical computing and graphic visualization.

Requires:
- [an R runtime](https://cran.rstudio.com/) 4.0-or-greater, any supported platform

### Recommended packages

Before consuming any data from AQTS, you'll need to be able issue HTTP requests to the public APIs and serialize data to/from JSON.

Install these packages to enable basic communication with your AQTS server from your R environment.
```R
> install.packages("jsonlite") # Fast JSON parser
> install.packages("httr") # Hadley's nice HTTP requests library
```

## Simple Hello-world for AQTS

```R
library(jsonlite)
library(httr)

timeseries$connect("https://myserver", "myusername", "mypassword")

# Fetch time-aligned data for one year of data
data = timeseries$getTimeSeriesData(c("Discharge.Working@Location","Stage.Working@Location"),
                                      queryFrom = "2012-01-01T00:00:00Z",
                                      queryTo   = "2013-01-01T00:00:00Z")

# Plot Stage vs Discharge
plot(data$Points$NumericValue1, jdata$Points$NumericValue2, log = "xy",
       xlab = data$TimeSeries$Identifier[1],
       ylab = data$TimeSeries$Identifier[2])
```

## Detailed documentation is on the wiki page

See this repo's [R wiki page](https://github.com/AquaticInformatics/examples/wiki/R-integration) for more detailed examples.
