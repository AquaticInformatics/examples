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
            { id: "Loc_ID",      dataType: tableau.dataTypeEnum.string },	
            { id: "Time",        dataType: tableau.dataTypeEnum.datetime, alias: "Time",      columnRole: "measure" } ];
            //create table columns for each of the time series: V for value, G for grade, A for approval
            for (var i=0; i<AQTSDescs.length; ++i) {
                var tsguid = AQTSDescs[i].UniqueId;
                var tsname = AQTSDescs[i].Identifier;
                time_series_pts_cols.push(
                { id: 'V_' + tsguid, dataType: tableau.dataTypeEnum.float,  alias: tsname,                columnRole: "measure" }, 
                { id: 'G_' + tsguid, dataType: tableau.dataTypeEnum.string, alias: "Grade: " + tsname,    columnRole: "dimension" }, 
                { id: 'A_' + tsguid, dataType: tableau.dataTypeEnum.string, alias: "Approval: " + tsname, columnRole: "dimension" } );
            }

        //tell tableau to create a left join on the two tables
        var joinInfo = {
            alias: "Joined AQUARIUS Data",
            tables: [ {id: "Location", alias: "AQUARIUS Locations"},
                      {id: "Points", alias: "AQUARIUS Time Series Points"} ],
            joins: [ {left:  {tableAlias: "AQUARIUS Locations", columnId: "Loc_ID"},
                      right: {tableAlias: "AQUARIUS Time Series Points", columnId: "Loc_ID"},
                      joinType: "left" } ]
            };

        //give the schema back to tableau
        schemaCallback(
            [ { id: "Location",   columns: location_cols,    alias: "AQUARIUS Locations" },
              { id: "Points",     columns: time_series_pts_cols,  alias: "AQUARIUS Time Series Points" } ],
            [joinInfo] );
    };

    //Build a lookup table for grades, since grade names are not returned with the data
    var GradesLookup = {};
    myConnector.init = function(initCallback) {
        if (tableau.phase == tableau.phaseEnum.gatherDataPhase) {
            var connectionInfo = JSON.parse(tableau.connectionData);  //params passed from interactive phase
            var AQServer = connectionInfo.server;
            var AQToken =  tableau.password;

            var URL = 'http://' + AQServer + '/aquarius/publish/v2/getgradelist?token=' + AQToken;
            $.getJSON(URL)
                .done(function(data) {
                    for (var i=0; i<data.Grades.length; i++) {
                        GradesLookup[data.Grades[i].Identifier] = data.Grades[i].DisplayName;
                    }
                })
                .fail(function( jqxhr, textStatus, error ) {
                    tableau.log( "Request Failed: " + textStatus + ", " + error );
                });
        }
        initCallback();
    };

    //This is a helper function that aligns values for multiple time series into the same 
    //table rows, based on location and timestamp.
    //-- A better version of this function would do point interpolation instead.
    function synchronizePoints(pointsData, tableData) {

        var pi = 0, ti = 0;
        for (pi = 0; pi < pointsData.length; ++pi) {
            var time = pointsData[pi].Time;
            var locid = pointsData[pi].Loc_ID;
            
            //find a matching timestamp row, or the insert point
            while (ti < tableData.length && tableData[ti].Time < time ) {ti++;}
            //find a matching location row, or the insert point
            while (ti < tableData.length && tableData[ti].Time == time && tableData[ti].Loc_ID != locid) {ti++;}
            
            if (ti < tableData.length && tableData[ti].Time == time && tableData[ti].Loc_ID == locid) {
                //found a matching row - merge
                $.extend( tableData[ti], pointsData[pi] );
            }
            else {//no match - insert a new row
                tableData.splice(ti, 0, pointsData[pi]);
            }
        }
        return tableData;
    }

    //Helper function to convert AQUARIUS json timeseries data into tableau table rows
    function parseAQUARIUSData (data) {
        var TableRows = [];
        
        var points = data.Points;
        var approvals = data.Approvals;
        var grades = data.Grades;
        var qualifiers = data.Qualifiers;	

        approvals.sort(function(a, b) {  //sort approvals by start time
            return (a.StartTime > b.StartTime) ? 1 : (a.StartTime < b.StartTime) ? -1 : 0; });		
        grades.sort(function(a, b) {  //sort grades by start time
            return (a.StartTime > b.StartTime) ? 1 : (a.StartTime < b.StartTime) ? -1 : 0; });
        qualifiers.sort(function(a, b) {  //sort qualifiers by start time
            return (a.StartTime > b.StartTime) ? 1 : (a.StartTime < b.StartTime) ? -1 : 0; });

        var pi = 0,  ai = 0, qi = 0, gi = 0;

        //Process each point
        for (pi = 0; pi < points.length; ++pi) {
            var ptime = points[pi].Timestamp;
            var pvalue = points[pi].Value.Numeric;

            //Find the approval for this point
            var papproval = null, papprovalnum = null;
            if (approvals.length > 0) { // find approval for this point (not guaranteed to have one)
                while (ptime >= approvals[ai].EndTime && ai < approvals.length-1) { ++ai; } 
                if (ptime >= approvals[ai].StartTime && ptime < approvals[ai].EndTime) {
                    papprovalnum = approvals[ai].ApprovalLevel;
                    papproval = approvals[ai].LevelDescription;
                }
            }
            //Find the grade for this point
            var pgrade = null, pgaradenum = null;
            if (grades.length > 0) { // find grade for this point (not guaranteed to have one)
                while (ptime >= grades[gi].EndTime && gi < grades.length-1) { ++gi; } 
                if (ptime >= grades[gi].StartTime && ptime < grades[gi].EndTime) {
                    pgradenum = grades[gi].GradeCode;
                    pgrade = GradesLookup[pgradenum];  // todo: would be nice if this was in the data...
                }
            }
            //Find the FIRST qualifier for this point -- THIS NEEDS WORK
            var pqualifier = null;
            if (qualifiers.length > 0) { // find qualifier for this point (not guaranteed to have one)
                while (ptime >= qualifiers[qi].EndTime && qi < qualifiers.length-1) { ++qi; } 
                if (ptime >= qualifiers[qi].StartTime && ptime < qualifiers[qi].EndTime) {
                    pqualifier = qualifiers[qi].Identifier;
                }
            }
            //construct the tableau data
            var tablerow = {};
            tablerow['Loc_ID'] =            data.LocationIdentifier;
            tablerow['Time'] =              ptime.replace('T',' ').substring(0,19);  //convert to supported time format
            tablerow['V_'+ data.UniqueId] = pvalue;
            tablerow['G_'+ data.UniqueId] = pgrade;
            tablerow['A_'+ data.UniqueId] = papproval;
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
                var tscount = 0;
                for (var i=0; i<AQTSDescs.length; ++i) {
                    //Get the data for each time series
                    var URL = 'http://' + AQServer + '/aquarius/publish/v2/gettimeseriescorrecteddata?token=' + AQToken 
                                + '&timeseriesuniqueid=' + AQTSDescs[i].UniqueId + '&includegapmarkers=true' + '&queryfrom=' + AQFromTime;
                    $.getJSON(URL)
                    .done(function(data) {
                        var pointsData = [];
                        if (data.Points) {
                            pointsData = parseAQUARIUSData(data);
                        }
                        //Count time series here since we are asynchronous
                        tscount++;
                        if (tscount == 1) {
                            tableData = pointsData; // #1 is the master
                        } else {
                            tableData = synchronizePoints(pointsData, tableData);
                        }
                        if (tscount == AQTSDescs.length) { 
                            table.appendRows(tableData);
                            dataCallback(); //done
                        }
                    })
                    .fail(function( jqxhr, textStatus, error ) {
                        tableau.log( "Request Failed: " + textStatus + ", " + error );
                        tableau.abortWithError("Error getting time series points.");
                    });			
                }		
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