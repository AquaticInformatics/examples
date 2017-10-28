//----------------------------------
// AQUARIUS Tableau Data Connector
//----------------------------------
(function() {
    var myConnector = tableau.makeConnector();
	
	myConnector.init = function(initCallback) {
		if (!tableau.password && tableau.phase == tableau.phaseEnum.gatherDataPhase) {
			tableau.abortForAuth("I need that token goodness!");
		}
		
		initCallback();
	}

    // DATA GATHERING PHASE OF THE CONNECTOR --------------------------------------------------------------

    //PREPARE THE TABLE SCHEMA For TABLEAU
    myConnector.getSchema = function(schemaCallback) {
         
        var AQTSDescs =    JSON.parse(tableau.connectionData).timeserieslist;  //params passed from interactive phase
        
        // Schema for locations
        var location_cols = [
            { id: "LocationIdentifier",	dataType: tableau.dataTypeEnum.string, alias: "Location Identifier" },
            { id: "LocationName", 		dataType: tableau.dataTypeEnum.string, alias: "Location Name" },
            { id: "LocationType", 		dataType: tableau.dataTypeEnum.string, alias: "Location Type", columnRole: "dimension" },
            { id: "Latitude",     		dataType: tableau.dataTypeEnum.float,  alias: "Latitude",  columnRole: "dimension" },
            { id: "Longitude", 	  		dataType: tableau.dataTypeEnum.float,  alias: "Longitude", columnRole: "dimension" },
            { id: "Elevation", 	  		dataType: tableau.dataTypeEnum.float,  alias: "Elevation", columnRole: "dimension" },
            { id: "ElevationUnits",		dataType: tableau.dataTypeEnum.string, alias: "Elevation Units", },
            { id: "UtcOffsetHours",		dataType: tableau.dataTypeEnum.float,  alias: "UTC Offset Hours", columnRole: "measure" }
		];

		// Schema for time series info
		var time_series_cols = [
			{ id: "TimeSeriesIdentifier", dataType: tableau.dataTypeEnum.string, alias: "Time Series Identifier" },
			{ id: "LocationIdentifier", dataType: tableau.dataTypeEnum.string, alias: "Location Identifier" },
			{ id: "Parameter", dataType: tableau.dataTypeEnum.string, columnRole: "dimension" },
			{ id: "Unit", dataType: tableau.dataTypeEnum.string, columnRole: "dimension" },
			{ id: "Label", dataType: tableau.dataTypeEnum.string },
			{ id: "Comment", dataType: tableau.dataTypeEnum.string },
			{ id: "Description", dataType: tableau.dataTypeEnum.string },
			{ id: "UtcOffsetHours", dataType: tableau.dataTypeEnum.float, alias: "UTC Offset Hours", columnRole: "measure" },
			{ id: "LastModified", dataType: tableau.dataTypeEnum.datetime, alias: "Last Modified", columnRole: "measure" },
			{ id: "RawStartTime", dataType: tableau.dataTypeEnum.datetime, alias: "Raw Start Time", columnRole: "measure" },
			{ id: "RawEndTime", dataType: tableau.dataTypeEnum.datetime, alias: "Raw End Time", columnRole: "measure" },
			{ id: "TimeSeriesType", dataType: tableau.dataTypeEnum.string, alias: "Time Series Type", columnRole: "dimension" },
			{ id: "ComputationIdentifier", dataType: tableau.dataTypeEnum.string, alias: "Computation Identifier", columnRole: "dimension" },
			{ id: "ComputationPeriodIdentifier", dataType: tableau.dataTypeEnum.string, alias: "Computation Period Identifier", columnRole: "dimension" },
			{ id: "SubLocationIdentifier", dataType: tableau.dataTypeEnum.string, alias: "SubLocation Identifier", columnRole: "dimension" }
		];

        // Schema for time series points
        var time_series_pts_cols = [           
            { id: "Time",        dataType: tableau.dataTypeEnum.datetime, alias: "Time", columnRole: "measure" } ];
            //create table columns for each of the time series: 
            for (var i = 0; i < AQTSDescs.length; ++i) {
                var tsname = AQTSDescs[i].Identifier;
                var locationIdentifier = AQTSDescs[i].LocationIdentifier;
                var propertySuffix = i + 1;

                time_series_pts_cols.push(
                    { id: 'Loc_ID' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: locationIdentifier },
                    { id: 'TS_ID' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: tsname },
                    { id: 'Value' + propertySuffix, dataType: tableau.dataTypeEnum.float, alias: tsname, columnRole: "measure" }, 
                    { id: 'GradeName' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: "Grade: " + tsname,    columnRole: "dimension" }, 
                    { id: 'ApprovalName' + propertySuffix, dataType: tableau.dataTypeEnum.string, alias: "Approval: " + tsname, columnRole: "dimension" } );
            }

        //give the schema back to tableau
        schemaCallback(
            [ { id: "Location",   columns: location_cols,    alias: "AQUARIUS Locations" },
			  { id: "TimeSeries", columns: time_series_cols, alias: "AQUARIUS Time Series" },
              { id: "Points",     columns: time_series_pts_cols,  alias: "AQUARIUS Time Series Points" } ],
            [] );
    };
	
	// Convert AQTS ISO 8601 timestamps to Tableau format
	function convertIso8601Timestamp(isoText) {
		// AQTS ISO-8601 timestamps with timezone
		// 000000000011111111112222222222333
		// 012345678901234567890123456789012
		// yyyy-MM-ddTHH:mm:ss.SSSSSSS+zH:zM
		
		// Simply dropping the timezone (rather than applying the offset) is often OK.
		// yyyy-MM-dd HH:mm:ss.SSS
		return isoText.replace('T', ' ').substring(0, 23);
	}

    //Helper function to convert AQUARIUS json timeseries data into tableau table rows
    function parseAQUARIUSData (propertySuffix, data) {
        var TableRows = [];

        var points = data.Points;
        var timeSeriesList = data.TimeSeries;

        var pi = 0;

        // Process each point
        for (pi = 0; pi < points.length; ++pi) {
            var point = points[pi];

            //construct the tableau data
            var tablerow = {};
			
            tablerow['Time'] = convertIso8601Timestamp(point.Timestamp);

            var tsIndex;
            for (tsIndex = 0; tsIndex < timeSeriesList.length; ++tsIndex) {
                var timeSeries = timeSeriesList[tsIndex];
				var tsSuffix = (tsIndex+1).toString();
                var columnSuffix = propertySuffix.length ? propertySuffix : tsSuffix;

                tablerow['Loc_ID'       + columnSuffix] = timeSeries.LocationIdentifier;
                tablerow['TS_ID'        + columnSuffix] = timeSeries.Identifier;
                tablerow['Value'        + columnSuffix] = point['NumericValue' + tsSuffix];
                tablerow['GradeName'    + columnSuffix] = point['GradeName'    + tsSuffix];
                tablerow['ApprovalName' + columnSuffix] = point['ApprovalName' + tsSuffix];
            }

            TableRows.push(tablerow);                         
        }
        return TableRows;
    }

    //Get the table data from AQUARIUS and give it to Tableau
    myConnector.getData = function(table, dataCallback) {
        
        var connectionInfo = JSON.parse(tableau.connectionData);  //params passed from interactive phase
        var AQPublishUrl = connectionInfo.publishUrl;
        var AQFolderPath = connectionInfo.folder;
        var AQFromTime =   connectionInfo.queryfrom;
        var AQToTime =     connectionInfo.queryto;
		var TimeAlignedPoints = connectionInfo.timeAlignedPoints;
        var AQTSDescs =    connectionInfo.timeserieslist;
        var AQToken =      tableau.password;
        var tableData = [];
		
		// Set the auth header on every call
		$.ajaxSetup({
			beforeSend: function (xhr) {
				xhr.setRequestHeader('X-Authentication-Token', AQToken);
			}
		});
		
		getTimeAlignedPoints = function(tsUniqueIds, queryFrom, queryTo, propertySuffix, completionCallback) {
			
			$.getJSON(AQPublishUrl + '/GetTimeSeriesData', {TimeSeriesUniqueIds: tsUniqueIds.join(), QueryFrom: AQFromTime, QueryTo: AQToTime})
			.done(function (data) {
				var pointsData = [];
				if (data.Points) {
					pointsData = parseAQUARIUSData(propertySuffix, data);
				}

				completionCallback(pointsData);
			})
			.fail(function( jqxhr, textStatus, error ) {
				if (jqxhr.status === 500){
					var errorResponse = JSON.parse(jqxhr.responseText);
					
					if (errorResponse.ResponseStatus.ErrorCode == "InvalidOperationException" && errorResponse.ResponseStatus.Message == "Sequence contains no elements") {
						// HACK: Workaround for AQ-21916, when the master time-series has no points
						completionCallback([]);
						return;
					}
				}
				
				tableau.log("Request GetTimeSeriesData Failed: " + textStatus + ", " + error );
				tableau.abortWithError("Error getting time series points.");
			});
		}
		
		getTimeSeriesPoints = function(tsUniqueIds, queryFrom, queryTo, timeAlignedPoints, completionCallback) {
			if (timeAlignedPoints) {
				getTimeAlignedPoints(tsUniqueIds, queryFrom, queryTo, "", completionCallback);
				return;
			}
			
			var mergedPoints = [];
			var receivedCount = 0;
			for (var i=0; i < tsUniqueIds.length; ++i) {
				getTimeAlignedPoints([tsUniqueIds[i]], queryFrom, queryTo, (i+1).toString(), function (pointsData){
					mergedPoints = mergedPoints.concat(pointsData);
					++receivedCount;
					
					if (receivedCount === tsUniqueIds.length) {
						mergedPoints.sort(function(a, b) {  
							return (a.Time > b.Time) ? 1 : (a.Time < b.Time) ? -1 : 0;
						});
						
						// Now that the points are sorted, coalesce all values with identical timestamp strings
						for (var j = 0; j < mergedPoints.length - 1; ++j) {
							var row1 = mergedPoints[j];
							var row2 = mergedPoints[j+1];
							
							if (row1.Time == row2.Time) {
								// Merge the second row into the first
								for (var attrname in row2) {
									row1[attrname] = row2[attrname];
								}
								
								// Remove row2 from the array
								mergedPoints.splice(j+1, 1);
								
								// Adjust the loop counter
								--j;
							}
						}
						
						completionCallback(mergedPoints);
					}
				});
			}
		}

        // LOCATIONS TABLE
        if (table.tableInfo.id == "Location") {
            //Get the list of locations in the given folder
			var params = {};
			if (AQFolderPath) { params['locationFolder'] = AQFolderPath; } //otherwise all locations
			$.getJSON(AQPublishUrl + '/GetLocationDescriptionList', params)
            .done(function(data) {
                var locations = data.LocationDescriptions;
                if (!locations) { dataCallback(); }
                else {
                    for (var i=0; i < locations.length; ++i) {
                        //For each location, get its metadata  (CAN ELIMINATE THIS STEP IN LATEST AQ VERSION)
                        var locIdentifier = locations[i].Identifier;
                        $.getJSON(AQPublishUrl + '/GetLocationData', {LocationIdentifier: locIdentifier})
                        .done(function(data) {
                            tableData.push({
                                'LocationName': data.LocationName,
                                'LocationIdentifier': data.Identifier,
                                'LocationType': data.LocationType,
                                'Latitude': data.Latitude,
                                'Longitude': data.Longitude,
								'Elevation': data.Elevation,
								'ElevationUnits': data.ElevationUnits,
								'UtcOffsetHours': data.UtcOffset
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

		// TIME SERIES TABLE
		else if (table.tableInfo.id == "TimeSeries") {
			var rows = [];
			
			$.each(AQTSDescs, function(index, data) {
				rows.push({
					'TimeSeriesIdentifier'			: data.Identifier,
					'LocationIdentifier' 			: data.LocationIdentifier,
					'Parameter' 					: data.Parameter,
					'Unit' 							: data.Unit,
					'Label' 						: data.Label,
					'Comment' 						: data.Comment,
					'Description' 					: data.Description,
					'UtcOffsetHours' 				: data.UtcOffset,
					'LastModified' 					: convertIso8601Timestamp(data.LastModified),
					'RawStartTime' 					: convertIso8601Timestamp(data.RawStartTime),
					'RawEndTime' 					: convertIso8601Timestamp(data.RawEndTime),
					'TimeSeriesType' 				: data.TimeSeriesType,
					'ComputationIdentifier' 		: data.ComputationIdentifier,
					'ComputationPeriodIdentifier'	: data.ComputationPeriodIdentifier,
					'SubLocationIdentifier' 		: data.SubLocationIdentifier
				});
			});

			table.appendRows(rows);
			dataCallback();
		}
		
        // TIME SERIES POINTS TABLE
        else if (table.tableInfo.id == "Points") {
            if (!AQTSDescs) { dataCallback(); }	
            else {
                var tsUniqueIds = AQTSDescs.map(function (item) {
                    return item.UniqueId;
                });
				
				getTimeSeriesPoints(tsUniqueIds, AQFromTime, AQToTime, TimeAlignedPoints, function (pointsData){
					table.appendRows(pointsData);
					dataCallback(); //done
				});
            }
        }
    };

     myConnector.shutdown = function(shutdownCallback) {
        if (tableau.password && tableau.phase == tableau.phaseEnum.gatherDataPhase) {
            // Release the AQUARIUS session token
            $.ajax({
				type: "DELETE",
				url: JSON.parse(tableau.connectionData).publishUrl + '/session',
				success: function (data) {
					tableau.password = null;
					$('#loginstatus').text("Disconnected");
				},
				error: function (xhr, ajaxOptions, thrownError) {
					tableau.log("Error during AQTS disconnection: " + xhr.responseText + "\n" + thrownError);
				}
            });
        }
		
        shutdownCallback();
    };

    tableau.registerConnector(myConnector);
})();
