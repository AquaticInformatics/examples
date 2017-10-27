//-----------------------------------------------------
// AQUARIUS Tableau Data Connector - INTERACTIVE PHASE 
//----------------------------------------------------
$(document).ready(function() {

	promptToAuthenticate = function() {
		$('#loginstatus').text("Enter valid connection credentials.");
		$('#loginstatus').css({'color': 'red'});
		$('#AQUser').focus();
	}
	
	parseUri = function(uriText) {
		// This stunt works across all browsers, no external JS library required!
		var uri = document.createElement('a');
		uri.href = uriText;
		
		return uri;
	}
	
	sanitizeServerName = function(serverName) {
		if (!/^https?:\/\//i.test(serverName)) {
			// When no scheme exists, assume http://
			serverName = 'http://' + serverName;
		}
		
		return serverName;
	}
	
    var AQToken, AQFolderPath, AQPublishUrl;
	
    //ON LOGIN SUBMIT Get an AQUARIUS token
    $("#LoginForm").submit(function(e) {
        e.preventDefault(); //no actual form submit
		
        var AQServer = $('#AQServer').val().trim();
        var AQUser = $('#AQUser').val().trim();
        var AQPassword = $('#AQPassword').val().trim();
		
        if (!AQServer || !AQUser || !AQPassword) {
			promptToAuthenticate();
			return;
		}

		AQServer = sanitizeServerName(AQServer);
	
		var uri = parseUri(AQServer);
		
		AQPublishUrl = uri.protocol + "//" + uri.hostname + '/AQUARIUS/Publish/v2';
		
		$.ajax({
			type: "POST",
			url: AQPublishUrl + '/session',
			data: { "Username": AQUser, "EncryptedPassword": AQPassword},
			dataType: 'text',
			success: function (data) {
				AQToken = data;
				$('#loginstatus').text("Connected");
				$('#loginstatus').css({'color': 'black'});
			},
			error: function (xhr, ajaxOptions, thrownError) {
				tableau.log("Error: " + xhr.responseText + "\n" + thrownError);
				tableau.abortWithError("Error authenticating to " + AQServer);
			}
		});
    }); 

    //ON FOLDER SUBMIT Get list of locations in folder and fill the selection box
    $("#FolderSelectForm").submit(function(e) {
        e.preventDefault(); //no actual form submit
		
		if (!AQToken) {
			promptToAuthenticate();
			return;
		}
		
        AQFolderPath = $('#AQFolder').val().trim();
		
		var params = {};
        if (AQFolderPath) { params['LocationFolder'] = AQFolderPath; } //otherwise all locations
        
		$.getJSON(AQPublishUrl + '/GetLocationDescriptionList', params)
        .done(function(data) {
            var descriptions = data.LocationDescriptions;
            $("#LocationList").empty();
            if (descriptions) {
                //sort by location name:
                descriptions.sort(function(a, b) {  
                    return (a.Name > b.Name) ? 1 : (a.Name < b.Name) ? -1 : 0;
                });

                //build list of locations:
                $.each(descriptions, function(index, description) {
                    $('#LocationList').append($('<option>').text(description.Name).val(description.Identifier)); 
                });
            }
        })
        .fail(function( jqxhr, textStatus, error ) {
            tableau.log( "Request Failed: " + textStatus + ", " + error );
        });
    });

    //ON LOCATION SELECT Get list of time series for location and fill the selection box
    $("#LocationList").change(function() {
		if (!AQToken)
			return;
		
        var LocationID = $(this).val().trim();
        $.getJSON(AQPublishUrl + '/GetTimeseriesDescriptionList', {LocationIdentifier: LocationID})
        .done(function(data) {
            var descriptions = data.TimeSeriesDescriptions;
            $("#TimeSeriesList").empty();
            if (descriptions) {
                //sort by time series identifier:
                descriptions.sort(function(a, b) {
                    return (a.Identifier > b.Identifier) ? 1 : (a.Identifier < b.Identifier) ? -1 : 0;
                });

                //build list of time series:
                $.each(descriptions, function(index, description) {
                    $('#TimeSeriesList').append($('<option>').text(description.Identifier).data("description", description));  
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
    $("#FetchButton").click(function () {
		if (!AQToken) {
			promptToAuthenticate();
			return;
		}

        var AQFromTime = $('#AQFromTime').val().trim();
        var AQToTime = $('#AQToTime').val().trim();
		var TimeAlignedPoints = $('#TimeAlignCheckbox').is(':checked');
		
        var SelectedTSIDs = [];
        $('#SelectedTSList option').each(function() {
            SelectedTSIDs.push($(this).data("description"));
        });

        //Do not continue if no time series selected:
        if (SelectedTSIDs.length <= 0) {
            return;
        }

        //Build string passed to data gathering phase
        tableau.connectionName = "AQUARIUS Web Connector";
        tableau.password = AQToken;
       
        tableau.connectionData = JSON.stringify({
            'publishUrl': AQPublishUrl,
            'folder': AQFolderPath,
            'queryfrom': AQFromTime,
            'queryto': AQToTime,
			'timeAlignedPoints' : TimeAlignedPoints,
            'timeserieslist': SelectedTSIDs
        }); 

        //Tell Tableau we are finished the Interactive phase.
        tableau.submit();
    });

	if (tableau.connectionData) {
		// Restore the previously used connection data when it exists 
		$('#loginstatus').text("");
		var connectionInfo = JSON.parse(tableau.connectionData);  //params passed from interactive phase
		
		AQPublishUrl = connectionInfo.publishUrl;
		var uri = parseUri(AQPublishUrl);
		
		$('#AQServer').val(uri.protocol + "//" + uri.hostname);
		$('#AQFolderPath').val(connectionInfo.folder);
		$('#AQFromTime').val(connectionInfo.queryfrom);
		$('#AQToTime').val(connectionInfo.queryto);
		$('#TimeAlignCheckbox').prop('checked', connectionInfo.timeAlignedPoints);
		
		$.each(connectionInfo.timeserieslist, function(index, description) {
                    $('#SelectedTSList').append($('<option>').text(description.Identifier).data("description", description));  
                });
		
		if (tableau.password) {
			AQToken = tableau.password;
			
			// Set the auth header on every call
			$.ajaxSetup({
				beforeSend: function (xhr) {
					xhr.setRequestHeader('X-Authentication-Token', AQToken);
				}
			});
		} else {
			AQToken = null;
		}
		
		$('#AQFromTime').focus();
	} else {
		//Initialize the UI
		AQToken = null;
		$('#AQServer').val(location.hostname); //set default server name to current host
		
		promptToAuthenticate();
	}

});
