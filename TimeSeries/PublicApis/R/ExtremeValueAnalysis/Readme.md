[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FR%2FExtremeValueAnalysis)

This R-based Shiny app can be used to explore extreme values of a time-series within a web-page.

## Requirements

```R
install.packages("shiny")
install.packages("plotly")
install.packages("DT")
install.packages("extRemes")
install.packages("hydroTSM")
```

## Configuration

All the configuration options are set at the top of the `app.R` file.

```R
# Configuration values for this script
config = list(
  server = "youraqtsserver", username = "admin", password = "admin",    # AQTS credentials for your server
  timeSeriesName = "Discharge.Working@Reporting01",                     # The time-series to analyze
  eventPeriodStartDay = "2002-01-01", eventPeriodEndDay = "2015-12-31") # The period to analyze
```

| Property | Required? | Description |
| ---|---|--- |
| server | Yes |The AQTS server name, as a DNS name, or an IP address string. If no scheme is supplied, `http://` will be used. |
| username, password | Yes | The AQTS credentials to use to retreieve data. |
| timeSeriesName | Yes| The time-series to analyze for flow duration. |
| eventPeriodStartDay, eventPeriodEndDay | Yes | Defines the event period to analyze. |

## Sample output

![Extreme Value Analysis](../images/ExtremeValueAnalysis.png "Extreme Value Analysis")
