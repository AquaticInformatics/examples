# Sample python code to Public API course
# Requires python 2.7+
# Install required dependencies via: $ pip install requests pytz pyrfc3339

import subprocess
import os
import requests
from requests.exceptions import HTTPError
import pyrfc3339
from datetime import datetime
import re


def create_endpoint(hostname, root_path):
    prefix = "http://"
    if hostname.startswith("http://") or hostname.startswith("https://"):
        prefix = ""
    return "{0}{1}{2}".format(prefix, hostname, root_path)


def response_or_raise(response):
    if response.status_code >= 400:
        json = response.json
        if isinstance(json, dict) and json.has_key('ResponseStatus'):
            http_error_msg = u'%s WebService Error: %s(%s) for url: %s' % (
                response.status_code, response.reason, json['ResponseStatus']['Message'], response.url)
            raise HTTPError(http_error_msg, response=response)

    response.raise_for_status()
    return response


class ModelNotFoundException(Exception):
    """Exception raised for errors in the input.

        Attributes:
            identifier -- The identifier that was not found
            message -- explanation of the error
        """

    def __init__(self, identifier, message):
        self.identifier = identifier
        self.message = message


class LocationNotFoundException(ModelNotFoundException):
    """"Raised when the location identifier is not known"""

    def __init__(self, identifier):
        super(ModelNotFoundException, self).__init__(identifier, "Location '{0}' not found.".format(identifier))


class TimeSeriesNotFoundException(ModelNotFoundException):
    """Raised when the time-series identifier cannot be found."""

    def __init__(self, identifier):
        super(ModelNotFoundException, self).__init__(identifier, "Time-series '{0}' not found.".format(identifier))


class TimeseriesSession(requests.sessions.Session):
    """
    A requests.Session object that:
    - Sends all requests to a base endpoint
    - Always raises an exception if any HTTP errors are detected.

    >>> session.get('/invalidroute') # Raises HTTPError (404)
    """

    def __init__(self, hostname, root_path, verify=True):
        super(TimeseriesSession, self).__init__()
        self.verify = verify
        self.base_url = create_endpoint(hostname, root_path)

    def get(self, url, **kwargs):
        r = super(TimeseriesSession, self).get(self.base_url + url, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def post(self, url, data=None, json=None, **kwargs):
        r = super(TimeseriesSession, self).post(self.base_url + url, data, json, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def put(self, url, data=None, **kwargs):
        r = super(TimeseriesSession, self).put(self.base_url + url, data, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def delete(self, url, **kwargs):
        r = super(TimeseriesSession, self).delete(self.base_url + url, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def set_session_token(self, token):
        self.headers.update({"X-Authentication-Token": token})

    def send_batch_requests(self, operation_name, requests, batch_size=100, verb="GET"):
        """
        Performs a batch of identical operations

        This method is useful for requesting large amounts of similar data from AQTS,
        taking advantage of ServiceStack's support for auto-batched requests.

        http://docs.servicestack.net/auto-batched-requests

        When you find that a public API only supports a 1-at-a-time approach, and your
        code needs to request thousands of items, the sendBatchRequests() method is the one to use.

        >>> # Request info for 3 locations.
        >>> # Single-request URL is GET /Publish/v2/GetLocationData?LocationIdentifer=loc1
        >>> # Operation name is "LocationDataServiceRequest"
        >>> requests = [{'LocationIdentifier': 'Loc1'}, {'LocationIdentifier': 'Loc2'}, {'LocationIdentifier': 'Loc3'}]
        >>> responses = client.publish.send_batch_requests("LocationDataServiceRequest", requests)
        :param operation_name: The name of operation, from the AQTS Metadata page, to perform multiple times. NOT the route, but the operation name.
        :param requests: A list of individual request objects
        :param batch_size: Optional batch size (defaults to 100 requests per batch)
        :param verb: Optional HTTP verb of the operation (defaults to "GET")
        :return: A list of all the responses.
        """
        url = "/json/reply/{0}[]".format(operation_name)

        # Split the list into batches
        batched_requests = [requests[i:i + batch_size] for i in range(0, len(requests), batch_size)]

        # Get the response batches
        batched_responses = [self.post(url, json=batch, headers={'X-Http-Method-Override': verb}).json() for batch in batched_requests]

        # Return the flattened response list
        return [response for batch in batched_responses for response in batch]


class timeseries_client:
    """
    A client wrapper for AQUARIUS Time-Series REST API consumption.

    Each of the public REST APIs is exposed as a timeseries_session object:

    publish      => An authenticated session to the /AQUARIUS/Publish/v2 endpoint
    acquisition  => An authenticated session to the /AQUARIUS/Acquisition/v2 endpoint
    provisioning => An authenticated session to the /AQUARIUS/Provisioning/v1 endpoint

    >>> timeseries = timeseries_client('localhost', 'admin', 'admin')
    >>> timeseries.publish.get('/session').json()
    {u'Username': u'admin', u'Locale': u'en', u'Token': u'GWVheAEXYDkJrqKWxFA1vQ2', u'CanConfigureSystem': True, u'IpAddress': u'172.16.1.90'}

    Session resources will be automatically cleaned up if used in WITH statement.

    >>> with timeseries_client('localhost', 'admin', 'admin') as timeseries:
    ...   parameters = timeseries.publish.get('/GetParameterList').json()["Parameters"]
    ...   print "There are {0} parameters".format(len(parameters))
    ...
    >>> # The session will be disconnected now, even if an exception was thrown in the body of the WITH statement.
    """

    def __init__(self, hostname, username, password, verify=True):
        # Create the three endpoint sessions
        self.publish = TimeseriesSession(hostname, "/AQUARIUS/Publish/v2", verify=verify)
        self.acquisition = TimeseriesSession(hostname, "/AQUARIUS/Acquisition/v2", verify=verify)
        self.provisioning = TimeseriesSession(hostname, "/AQUARIUS/Provisioning/v1", verify=verify)

        # Authenticate once
        self.configure_proxy()
        self.connect(username, password)

        # Cache the server version
        versionSession = TimeseriesSession(hostname, "/AQUARIUS/apps/v1", verify=verify)
        self.serverVersion = versionSession.get('/version').json()["ApiVersion"]

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception_value, exception_traceback):
        self.disconnect()

    def process_exists(self, process_name):
        call = 'TASKLIST', '/FI', 'imagename eq %s' % process_name
        # use buildin check_output right away
        output = subprocess.check_output(call).decode()
        # check in last line for process name
        last_line = output.strip().split('\r\n')[-1]
        # because Fail message could be translated
        return last_line.lower().startswith(process_name.lower())

    def configure_proxy(self):
        if any(key in os.environ for key in ['PYTHON_DISABLE_FIDDLER', 'http_proxy', 'https_proxy']) or os.name != 'nt':
            return

        if self.process_exists('Fiddler.exe'):
            os.environ['http_proxy'] = os.environ['https_proxy'] = '127.0.0.1:8888'

    def connect(self, username, password):
        """
        Authenticates the session with AQUARIUS.

        All subsequent requests to any public endpoint will be authenticated using the stored session token.
        """
        token = self.publish.post('/session', json={'Username': username, 'EncryptedPassword': password}).text
        self.publish.set_session_token(token)
        self.acquisition.set_session_token(token)
        self.provisioning.set_session_token(token)

    def disconnect(self):
        """Destroys the authenticated session"""
        self.publish.delete('/session')

    def isVersionLessThan(self, sourceVersion, targetVersion):
        """
        Is the source version strictly less than the target version.

        :param sourceVersion: The source version, in dotted.string notation
        :param targetVersion: If None, the connected server version is used
        :return: True if the source version is strictly less than the target version
        """
        if targetVersion is None:
            targetVersion = self.serverVersion

        def createIntegerVector(versionText):

            if versionText == '0.0.0.0':
                # Force developer versions to be treated as latest-n-greatest
                versionText = '9999.99'

            versions = [int(i) for i in versionText.split('.')]

            if len(versions) > 0 and 14 <= versions[0] <= 99:
                # Adjust the leading component to match the 20xx.y release convention
                versions[0] += 2000

            return versions

        target = createIntegerVector(targetVersion)
        source = createIntegerVector(sourceVersion)

        for i in range(len(source)):
            if i >= len(target):
                return False

            if source[i] < target[i]:
                return True

            if source[i] > target[i]:
                return False

        return len(source) < len(target)

    def isServerVersionLessThan(self, targetVersion):
        return self.isVersionLessThan(self.serverVersion, targetVersion)

    def iso8601(self, datetime):
        """Formats the datetime object as an ISO8601 timestamp"""
        return pyrfc3339.generate(datetime, microseconds=True)

    def datetime(self, text):
        """Parses the ISO8601 timestamp to a standard python datetime object"""
        return pyrfc3339.parse(text)

    def coerceQueryTime(self, querytime):
        """Coerces the timevalue into a best possible query time format"""
        if isinstance(querytime, datetime):
            if querytime.tzinfo is None:
                # Format naive date times as a local time
                return querytime.strftime('%Y-%m-%d %H:%M:%S.%f')
            else:
                # Format unambiguous times as ISO8061
                return self.iso8601(querytime)

        # Otherwise return the value as-is and hope for the best
        return querytime

    def toJSV(self, item):
        """
        Converts non-scalar GET request parameters into JSV format.

        Lists must be comma separated
        Dictionaries must be in {Name1:Value1,Name2:Value2} format

        https://github.com/ServiceStack/ServiceStack.Text/wiki/JSV-Format
        :param item: item to be converted into JSV format
        :return: the JSV representation of the item
        """
        if isinstance(item, list):
            # Concatenate all values with commas
            return '[' + ','.join([str(self.toJSV(i)) for i in item]) + ']'
        if isinstance(item, dict):
            # Concatenate all the name/value pairs
            return '{' + ','.join([k+':'+str(self.toJSV(item[k])) for k in item.keys()]) + '}'

        return item

    def getTimeSeriesUniqueId(self, timeSeriesIdentifier):
        """Gets the unique ID of a time-series"""
        parts = timeSeriesIdentifier.split('@')

        if len(parts) < 2:
            return timeSeriesIdentifier

        location = parts[1]

        # Get the descriptions from the location
        try:
            descriptions = self.publish.get(
                '/GetTimeSeriesDescriptionList', params={'LocationIdentifier': location}).json()[
                "TimeSeriesDescriptions"]
        except requests.exceptions.HTTPError as e:
            raise LocationNotFoundException(location)

        matches = [d for d in descriptions if d['Identifier'] == timeSeriesIdentifier]

        if len(matches) != 1:
            raise TimeSeriesNotFoundException(timeSeriesIdentifier)

        return matches[0]['UniqueId']

    def getLocationIdentifier(self, timeSeriesOrRatingModelIdentifier):
        """Extracts the location identifier from a 'Parameter.Label@Location' time-series or rating model identifier"""
        parts = timeSeriesOrRatingModelIdentifier.split('@')

        if len(parts) < 2:
            raise LocationNotFoundException(timeSeriesOrRatingModelIdentifier)

        return parts[1]

    def getLocationData(self, identifier):
        """Gets the location data"""
        return self.publish.get(
            "/GetLocationData", params={'LocationIdentifier': identifier}).json()

    def getLocationUniqueId(self, locationIdentifier):
        """Gets the location unique ID for the location"""
        if re.match('^[0-9a-f]{32}$', locationIdentifier):
            # Return existing GUIDs as-is
            return locationIdentifier

        locationData = self.getLocationData(locationIdentifier)

        return locationData['UniqueId']

    def getRatings(self, locationIdentifier, queryFrom=None, queryTo=None, inputParameter=None, outputParameter=None):
        ratingModelDescriptions = self.publish.get(
            "/GetRatingModelDescriptionList",
            params={
                'LocationIdentifier': locationIdentifier,
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'InputParameter': inputParameter,
                'OutputParameter': outputParameter
            }).json()['RatingModelDescriptions']

        # TODO: Get the rating curves in effect during those times
        return ratingModelDescriptions

    def getRatingModelOutputValues(self, ratingModelIdentifier, inputValues, effectiveTime=None, applyShifts=None):
        return self.publish.get(
            "/GetRatingModelOutputValues",
            params={
                'RatingModelIdentifier': ratingModelIdentifier,
                'InputValues': inputValues,
                'EffectiveTime': self.coerceQueryTime(effectiveTime),
                'ApplyShifts': applyShifts
            }).json()['OutputValues']

    def getFieldVisits(self, locationIdentifier, queryFrom=None, queryTo=None, activityType=None):
        fieldVisitDescriptions = self.publish.get(
            "/GetFieldVisitDescriptionList",
            params={
                'LocationIdentifier': locationIdentifier,
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'ActivityType': activityType
            }).json()['FieldVisitDescriptions']

        # TODO: Fetch details for each visit
        return fieldVisitDescriptions

    def getTimeSeriesDescriptions(self, locationIdentifier=None, parameter=None, publish=None, computationIdentifier=None, computationPeriodIdentifier=None, extendedFilters=None):
        return self.publish.get(
            "/GetTimeSeriesDescriptionList",
            params={
                'LocationIdentifier': locationIdentifier,
                'Parameter': parameter,
                'Publish': publish,
                'ComputationIdentifier': computationIdentifier,
                'ComputationPeriodIdentifier': computationPeriodIdentifier,
                'ExtendedFilters': self.toJSV(extendedFilters)
            }).json()['TimeSeriesDescriptions']

    def getTimeSeriesData(self, timeSeriesIds, queryFrom=None, queryTo=None, outputUnitIds=None, includeGapMarkers=None):
        if isinstance(timeSeriesIds, list):
            timeSeriesIds = [self.getTimeSeriesUniqueId(ts) for ts in timeSeriesIds]
        else:
            timeSeriesIds = self.getTimeSeriesUniqueId(timeSeriesIds)

        return self.publish.get(
            "/GetTimeSeriesData",
            params={
                'TimeSeriesUniqueIds': self.toJSV(timeSeriesIds),
                'TimeSeriesOutputUnitIds': self.toJSV(outputUnitIds),
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'IncludeGapMarkers': includeGapMarkers
            }).json()

    def getTimeSeriesCorrectedData(self, timeSeriesIdentifier, queryFrom=None, queryTo=None, getParts=None, includeGapMarkers=None):
        return self.publish.get(
            "/GetTimeSeriesCorrectedData",
            params={
                'TimeSeriesUniqueId': self.getTimeSeriesUniqueId(timeSeriesIdentifier),
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'GetParts': getParts,
                'IncludeGapMarkers': includeGapMarkers
            }).json()

    def getReportList(self):
        """Gets all the generated reports on the system"""
        return self.publish.get("/GetReportList").json()['Reports']

    def deleteReport(self, reportUniqueId):
        """Deletes the generated report from the system"""
        self.acquisition.delete("/attachments/reports/" + reportUniqueId)

    def uploadExternalReport(self, locationUniqueId, pathToFile, title, deleteDuplicateReports=False):
        """Uploads a file as external report to the given location"""
        if deleteDuplicateReports:
            # Get the current reports
            for r in [r for r in self.getReportList() if r['Title'] == title and not r['IsTransient']]:
                self.deleteReport(r['ReportUniqueId'])

        return self.acquisition.post("/locations/" + locationUniqueId + "/attachments/reports",
                                     params={'Title': title},
                                     files={'file': open(pathToFile, 'rb')}).json()

    def uploadFieldVisit(self, locationUniqueId, pathToFile):
        """Uploads a file as a field visit to the given location"""
        return self.acquisition.post("/locations/" + locationUniqueId + "/visits/upload/plugins",
                                     files={'file': open(pathToFile, 'rb')}).json()
