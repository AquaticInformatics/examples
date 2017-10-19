//-----------------------------------------------------
// AQUARIUS Tableau Data Connector - INTERACTIVE PHASE 
//----------------------------------------------------
$(document).ready(function() {

    var AQServer, AQToken, AQFolderPath;

    //ON LOGIN SUBMIT Get an AQUARIUS token
    $("#LoginForm").submit(function(e) {
        e.preventDefault(); //no actual form submit
        AQServer = $('#AQServer').val().trim();
        var AQUser = $('#AQUser').val().trim();
        var AQPassword = $('#AQPassword').val().trim();	
        if (AQServer) {
            var URL = 'http://' + AQServer + '/aquarius/publish/v2/getauthtoken?userName=' + AQUser + '&encryptedPassword=' + AQPassword;
            $.ajax({
            url: URL,
            dataType: 'text',
            success: function (data) {
                AQToken = data;  //authenticated
                $('#loginstatus').text("Connected");
            },
            error: function (xhr, ajaxOptions, thrownError) {
                tableau.log("Error: " + xhr.responseText + "\n" + thrownError);
                tableau.abortWithError("Error authenticating to " + AQServer);
            }
            });
        }
    });  //todo: get the folder hierarchy (when the API method exists) and create a dropdown tree

    //ON FOLDER SUBMIT Get list of locations in folder and fill the selection box
    $("#FolderSelectForm").submit(function(e) {
        e.preventDefault(); //no actual form submit
        AQFolderPath = encodeURIComponent( $('#AQFolder').val().trim() ); //handle spaces in folder names
        var URL = 'http://' + AQServer + '/aquarius/publish/v2/getlocationdescriptionlist?token=' + AQToken;
        if (AQFolderPath) { URL += '&locationfolder=' + AQFolderPath; } //otherwise all locations
        $.getJSON(URL)
        .done(function(data) {
            var descriptions = data.LocationDescriptions;
            $("#LocationList").empty();
            if (descriptions) {
                descriptions.sort(function(a, b) {  //sort by identifier
                    return (a.Name > b.Name) ? 1 : (a.Name < b.Name) ? -1 : 0; });
                $.each(descriptions, function(index, description) {
                    $('#LocationList').append($('<option>').text(description.Name).val(description.Identifier));  //build list of locations
                });
            }
        })
        .fail(function( jqxhr, textStatus, error ) {
            tableau.log( "Request Failed: " + textStatus + ", " + error );
        });
    });

    //ON LOCATION SELECT Get list of time series for location and fill the selection box
    $("#LocationList").change(function() {
        var LocationID = $(this).val().trim();
        var URL = 'http://' + AQServer + '/aquarius/publish/v2/gettimeseriesdescriptionlist?token=' + AQToken + '&locationidentifier=' + LocationID;
        $.getJSON(URL)
        .done(function(data) {
            var descriptions = data.TimeSeriesDescriptions;
            $("#TimeSeriesList").empty();
            if (descriptions) {
                descriptions.sort(function(a, b) {  //sort by identifier
                    return (a.Identifier > b.Identifier) ? 1 : (a.Identifier < b.Identifier) ? -1 : 0; });
                $.each(descriptions, function(index, description) {
                    $('#TimeSeriesList').append($('<option>').text(description.Identifier).data("description", description));  //build list of time series
                });
            }
        })
        .fail(function( jqxhr, textStatus, error ) {
            tableau.log( "Request Failed: " + textStatus + ", " + error );
        });
    });

    //ON ADD BUTTON CLICK Add selected time series to the list
    $("#AddTSButton").click(function() {
        $('#TimeSeriesList :selected').each(function() {
            var TSDesc = $(this).data("description");
            var TSName = $(this).text();

            if ($('#SelectedTSList option:contains('+ TSName +')').length == 0) {
                $('#SelectedTSList').append($('<option>').text(TSName).data("description", TSDesc));
            }
        });
    });

    //ON REMOVE BUTTON CLICK Remove selected time series from the list
    $("#RemoveTSButton").click(function() {
        $('#SelectedTSList :selected').each(function() {
            $(this).remove();
        });
    });

    //ON FETCH BUTTON CLICK Get the selected timeseries IDs and submit to the data gathering phase
    $("#FetchButton").click(function() {
        if (AQToken) {
            var AQFromTime = $('#AQFromTime').val().trim();
            var SelectedTSIDs = [];
            $('#SelectedTSList option').each(function() {
                SelectedTSIDs.push($(this).data("description"));
            });

            tableau.connectionName = "AQUARIUS Web Connector";
            tableau.password = AQToken;
            tableau.connectionData = JSON.stringify({ 'server':AQServer,            //string passed to data gathering phase
                                                      'folder':AQFolderPath,
                                                      'queryfrom':AQFromTime,
                                                      'timeserieslist':SelectedTSIDs }); 

            //Tell Tableau we are finished the Interactive phase.
            tableau.submit();
        }
    });

    //Initialize the UI
    $('#AQServer').val(location.hostname); //set default server name to current host
    $('#AQUser').val("admin");
    //$('#AQPassword').val("XXX"); 
    $('#AQPassword').focus();
});