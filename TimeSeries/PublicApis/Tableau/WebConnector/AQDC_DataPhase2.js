//----------------------------------
// AQUARIUS Tableau Data Connector
//----------------------------------
(function() {
    var myConnector = tableau.makeConnector();

    // DATA GATHERING PHASE OF THE CONNECTOR --------------------------------------------------------------

    //PREPARE THE TABLE SCHEMA For TABLEAU
    myConnector.getSchema = function(schemaCallback) {
         
        var AQTSDescs =    JSON.parse(tableau.connectionData).timeserieslist;  //params passed from interactive phase
        
        // Schema for locations
        var location_cols = [
            { id: "Loc_ID",       dataType: tableau.dataTypeEnum.string },
            { id: "LocationName", dataType: tableau.dataTypeEnum.string, alias: "Location Name" },
            { id: "LocationType", dataType: tableau.dataTypeEnum.string, alias: "Location Type" },
            { id: "Latitude",     dataType: tableau.dataTypeEnum.float,  alias: "Latitude",  columnRole: "dimension" },
            { id: "Longitude", 	  dataType: tableau.dataTypeEnum.float,  alias: "Longitude", columnRole: "dimension" } ];

        // Schema for time series info
        var time_series_pts_cols = [           
            { id: "Time",        dataType: tableau.dataTypeEnum.datetime, alias: "Time", columnRole: "measure" } ];
            //create table columns for each of the time series: 
            for (var i = 0; i < AQTSDescs.length; ++i) {
                var tsname = AQTSDescs[i].Identifier;
                var locationIdentifier = AQTSDescs[i].LocationIdentifier;
                var propertySuffix = i + 1;

                time_series_pts_cols.push(
                    { id: 'Loc_ID' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: locationIdentifier },
                    { id: 'NumericValue' + propertySuffix, dataType: tableau.dataTypeEnum.float, alias: tsname, columnRole: "measure" }, 
                    { id: 'GradeName' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: "Grade: " + tsname,    columnRole: "dimension" }, 
                    { id: 'ApprovalName' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: "Approval: " + tsname, columnRole: "dimension" } );
            }

        //give the schema back to tableau
        schemaCallback(
            [ { id: "Location",   columns: location_cols,    alias: "AQUARIUS Locations" },
              { id: "Points",     columns: time_series_pts_cols,  alias: "AQUARIUS Time Series Points" } ],
            [] );
    };

    //Helper function to convert AQUARIUS json timeseries data into tableau table rows
    function parseAQUARIUSData (data) {
        var TableRows = [];

        var points = data.Points;
        var timeSeriesList = data.TimeSeries;

        var pi = 0;

        //Process each point
        for (pi = 0; pi < points.length; ++pi) {
            var point = points[pi];

            //construct the tableau data
            var tablerow = {};
            tablerow['Time'] = point.Timestamp.replace('T', ' ').substring(0, 19);  //convert to supported time format

            var tsIndex;
            for (tsIndex = 0; tsIndex < timeSeriesList.length; ++tsIndex) {
                var timeSeries = timeSeriesList[tsIndex];
                var propertySuffix = tsIndex + 1;

                var locIdPropertyName = "Loc_ID" + propertySuffix;
                var valuePropertyName = 'NumericValue' + propertySuffix;
                var gradePropertyName = 'GradeName' + propertySuffix;
                var approvalPropertyName = 'ApprovalName' + propertySuffix;

                tablerow[locIdPropertyName] = timeSeries.LocationIdentifier;
                tablerow[valuePropertyName] = point[valuePropertyName];
                tablerow[gradePropertyName] = point[gradePropertyName];
                tablerow[approvalPropertyName] = point[approvalPropertyName];
            }

            TableRows.push(tablerow);                         
        }
        return TableRows;
    }

    //Get the table data from AQUARIUS and give it to Tableau
    myConnector.getData = function(table, dataCallback) {
        
        var connectionInfo = JSON.parse(tableau.connectionData);  //params passed from interactive phase
        var AQServer =     connectionInfo.server;
        var AQFolderPath = connectionInfo.folder;
        var AQFromTime =   connectionInfo.queryfrom;
        var AQTSDescs =    connectionInfo.timeserieslist;
        var AQToken =      tableau.password;
        var tableData = [];

        //LOCATIONS TABLE
        if (table.tableInfo.id == "Location") {
            //Get the list of locations in the given folder
            var URL = 'http://' + AQServer + '/aquarius/publish/v2/getlocationdescriptionlist?token=' + AQToken;
            if (AQFolderPath) { URL += '&locationfolder=' + AQFolderPath; } //otherwise all locations
            $.getJSON(URL)
            .done(function(data) {
                var locations = data.LocationDescriptions;
                if (!locations) { dataCallback(); }
                else {
                    for (var i=0; i < locations.length; ++i) {
                        //For each location, get its metadata  (CAN ELIMINATE THIS STEP IN LATEST AQ VERSION)
                        var locIdentifier = encodeURIComponent(locations[i].Identifier);
                        URL = 'http://' + AQServer + '/aquarius/publish/v2/getlocationdata?token=' + AQToken + '&LocationIdentifier=' + locIdentifier;
                        $.getJSON(URL)
                        .done(function(data) {
                            tableData.push({
                                'LocationName': data.LocationName,
                                'Loc_ID': data.Identifier,
                                'LocationType': data.LocationType,
                                'Latitude': data.Latitude,
                                'Longitude': data.Longitude
                            });
                            //This is asynchronous ajax, so do the final step in the innermost function
                            if (tableData.length == locations.length) {
                                table.appendRows(tableData);
                                dataCallback();	//done
                            }
                        })
                        .fail(function( jqxhr, textStatus, error ) {
                            tableau.log( "Request Failed: " + textStatus + ", " + error );
                            tableau.abortWithError("Error getting location info.");
                        });
                    }
                }
            })
            .fail(function( jqxhr, textStatus, error ) {
                tableau.log( "Request Failed: " + textStatus + ", " + error );
                tableau.abortWithError("Error getting location descriptions.");
            });
        }

        //TIME SERIES POINTS TABLE
        else if (table.tableInfo.id == "Points") {
            if (!AQTSDescs) { dataCallback(); }	
            else {
                var tsUniqueIds = AQTSDescs.map(function (item) {
                    return item.UniqueId;
                });

                var urlNew = 'http://' + AQServer + '/aquarius/publish/v2/GetTimeSeriesData?token=' + AQToken
                    + '&timeseriesuniqueids=' + tsUniqueIds.join() + '&includegapmarkers=true' + '&queryfrom=' + AQFromTime;

                $.getJSON(urlNew)
                    .done(function (data) {
                        var pointsData = [];
                        if (data.Points) {
                            pointsData = parseAQUARIUSData(data);
                        }

                        table.appendRows(pointsData);
                        dataCallback(); //done
                    })
                    .fail(function( jqxhr, textStatus, error ) {
                        tableau.log("Request GetTimeSeriesData Failed: " + textStatus + ", " + error );
                        tableau.abortWithError("Error getting time series points.");
                    });
            }
        }
    };

     myConnector.shutdown = function(shutdownCallback) {
        if (tableau.phase == tableau.phaseEnum.gatherDataPhase) {
            // todo: release the AQUARIUS session token
        }
        shutdownCallback();
    };

    tableau.registerConnector(myConnector);
})();
