# Sample python code to Public API course
# Requires python 2.7+
# Install required dependencies via: $ pip install requests pytz pyrfc3339

import requests
from requests.exceptions import HTTPError
import pyrfc3339


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

    def __init__(self, hostname, root_path):
        super(TimeseriesSession, self).__init__()
        self.base_url = create_endpoint(hostname, root_path)

    def get(self, url, **kwargs):
        r = super(TimeseriesSession, self).get(self.base_url + url, **kwargs)
        return response_or_raise(r)

    def post(self, url, data=None, json=None, **kwargs):
        r = super(TimeseriesSession, self).post(self.base_url + url, data, json, **kwargs)
        return response_or_raise(r)

    def put(self, url, data=None, **kwargs):
        r = super(TimeseriesSession, self).put(self.base_url + url, data, **kwargs)
        return response_or_raise(r)

    def delete(self, url, **kwargs):
        r = super(TimeseriesSession, self).delete(self.base_url + url, **kwargs)
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

    def __init__(self, hostname, username, password):
        # Create the three endpoint sessions
        self.publish = TimeseriesSession(hostname, "/AQUARIUS/Publish/v2")
        self.acquisition = TimeseriesSession(hostname, "/AQUARIUS/Acquisition/v2")
        self.provisioning = TimeseriesSession(hostname, "/AQUARIUS/Provisioning/v1")
        # Authenticate once
        self.connect(username, password)

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception_value, exception_traceback):
        self.disconnect()

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

    def iso8601(self, datetime):
        """Formats the datetime object as an ISO8601 timestamp"""
        return pyrfc3339.generate(datetime, microseconds=True)

    def datetime(self, text):
        """Parses the ISO8601 timestamp to a standard python datetime object"""
        return pyrfc3339.parse(text)

    def getTimeSeriesUniqueId(self, identifier):
        """Gets the unique ID of a time-series"""
        parts = identifier.split('@')

        if len(parts) < 2:
            return identifier

        location = parts[1]

        # Get the descriptions from the location
        try:
            descriptions = self.publish.get(
                '/GetTimeSeriesDescriptionList', params={'LocationIdentifier': location}).json()[
                "TimeSeriesDescriptions"]
        except requests.exceptions.HTTPError as e:
            raise LocationNotFoundException(location)

        matches = [d for d in descriptions if d['Identifier'] == identifier]

        if len(matches) != 1:
            raise TimeSeriesNotFoundException(identifier)

        return matches[0]['UniqueId']

    def getLocationData(self, identifier):
        """Gets the location data"""
        return self.publish.get(
            "/GetLocationData", params={'LocationIdentifier': identifier}).json()

    def getReportList(self):
        return self.publish.get("/GetReportList").json()['Reports']

    def deleteReport(self, reportUniqueId):
        self.acquisition.delete("/attachments/reports/" + reportUniqueId)

    def uploadExternalReport(self, locationUniqueId, pathToFile, title, deleteDuplicateReports=False):
        if deleteDuplicateReports:
            # Get the current reports
            for r in [r for r in self.getReportList() if r['Title'] == title and not r['IsTransient']]:
                self.deleteReport(r['ReportUniqueId'])

        return self.acquisition.post("/locations/" + locationUniqueId + "/attachments/reports",
                                     params={'Title': title},
                                     files={'file': open(pathToFile, 'rb')}).json()
