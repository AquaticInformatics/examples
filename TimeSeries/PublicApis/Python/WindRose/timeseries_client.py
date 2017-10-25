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


class timeseries_session(requests.sessions.Session):
    """
	A requests.Session object that:
	- Sends all requests to a base endpoint
	- Always raises an exception if any HTTP errors are detected.

	>>> session.get('/invalidroute') # Raises HTTPError (404)
	"""

    def __init__(self, hostname, root_path):
        super(timeseries_session, self).__init__()
        self.base_url = create_endpoint(hostname, root_path)

    def get(self, url, **kwargs):
        r = super(timeseries_session, self).get(self.base_url + url, **kwargs)
        return response_or_raise(r)

    def post(self, url, data=None, json=None, **kwargs):
        r = super(timeseries_session, self).post(self.base_url + url, data, json, **kwargs)
        return response_or_raise(r)

    def put(self, url, data=None, **kwargs):
        r = super(timeseries_session, self).put(self.base_url + url, data, **kwargs)
        return response_or_raise(r)

    def delete(self, url, **kwargs):
        r = super(timeseries_session, self).delete(self.base_url + url, **kwargs)
        return response_or_raise(r)

    def set_session_token(self, token):
        self.headers.update({"X-Authentication-Token": token})


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
        self.publish = timeseries_session(hostname, "/AQUARIUS/Publish/v2")
        self.acquisition = timeseries_session(hostname, "/AQUARIUS/Acquisition/v2")
        self.provisioning = timeseries_session(hostname, "/AQUARIUS/Provisioning/v1")
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
        descriptions = self.publish.get(
            '/GetTimeSeriesDescriptionList', params={'LocationIdentifier': location}).json()[
            "TimeSeriesDescriptions"]

        ts = [d for d in descriptions if d['Identifier'] == identifier][0]

        return ts['UniqueId']

    def getLocationData(self, identifier):
        """Gets the location data"""
        return self.publish.get(
            "/GetLocationData", params={'LocationIdentifier': identifier}).json()

    def getReportList(self):
        return self.publish.get("/GetReportList").json()['Reports']

    def deleteReport(self, reportUniqueId):
        self.acquisition.delete("/attachments/reports/" + reportUniqueId)

    def uploadExternalReport(self, locationUniqueId, pathToFile, title, deleteDuplicateReports = False):
        if deleteDuplicateReports:
            # Get the current reports
            for r in [r for r in self.getReportList() if r['Title'] == title and not r['IsTransient']]:
                self.deleteReport(r['ReportUniqueId'])

        return self.acquisition.post("/locations/" + locationUniqueId + "/attachments/reports",
                              params={'Title': title},
                              files={'file': open(pathToFile, 'rb')}).json()
