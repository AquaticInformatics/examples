# Sample python code to Public API course
# Requires python 3.7+
# Install required dependencies via: $ pip install requests pytz pyrfc3339

from datetime import datetime
from datetime import timedelta
from functools import total_ordering
import os
import platform
import pyrfc3339
import requests
from requests.exceptions import HTTPError
import re
import subprocess
from time import sleep


def create_endpoint(hostname, root_path):
    prefix = "http://"
    if hostname.startswith("http://") or hostname.startswith("https://"):
        prefix = ""
    return f"{prefix}{hostname}{root_path}"


def response_or_raise(response):
    if response.status_code >= 400:
        json = response.json()
        if isinstance(json, dict):
            error_summary = ''

            if 'ResponseStatus' in json:
                # AQTS & AQWP style of errors
                response_status = json['ResponseStatus']
                error_summary = f'{response_status.get("ErrorCode", "Unknown error code")} - {response_status.get("Message", "Unknown error message")}'
                error_details = response_status.get('Errors', [])

                if isinstance(error_details, list):
                    details = ", ".join([f'[{", ".join([f"{k}:{v}" for (k,v) in error.items()])}]' for error in error_details])
                    error_summary = f'{error_summary}{f": {details}" if details else ""}'

                error_summary = f' ({error_summary})'
            elif 'message' in json:
                # AQSamples style of errors
                error_summary = f' ({json.get("errorCode", "Unknown error code")} - {json["message"]})'

            http_error_msg = f"{response.status_code} WebService Error: {response.reason}{error_summary} for url: {response.url}"
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
        super().__init__(identifier, f"Location '{identifier}' not found.")


class TimeSeriesNotFoundException(ModelNotFoundException):
    """Raised when the time-series identifier cannot be found."""

    def __init__(self, identifier):
        super().__init__(identifier, f"Time-series '{identifier}' not found.")


@total_ordering
class ServerVersion(object):
    """
    A comparable version object. Any number of dots are supported.

    :param version_text: A dotted-integer version string. Eg. '20.3.43'
    """
    def __init__(self, version_text):
        self.version_text = version_text
        self._version_vector = self._create_integer_vector(version_text)

    def __repr__(self):
        return self.version_text

    def __eq__(self, other):
        return not self < other and not other < self

    def __lt__(self, other):
        if not isinstance(other, ServerVersion):
            return NotImplemented

        target = other._version_vector
        source = self._version_vector

        for i in range(len(source)):
            if i >= len(target):
                return False

            if source[i] < target[i]:
                return True

            if source[i] > target[i]:
                return False

        return len(source) < len(target)

    @staticmethod
    def _create_integer_vector(version_text):
        if version_text == '0.0.0.0':
            # Force developer versions to be treated as latest-n-greatest
            version_text = '9999.99'

        versions = [int(i) for i in version_text.split('.')]

        if len(versions) > 0 and 14 <= versions[0] <= 99:
            # Adjust the leading component to match the 20xx.y release convention used by AQUARIUS products
            versions[0] += 2000

        return versions


class RestSession(requests.sessions.Session):
    """
    A requests.Session object that:
    - Sends all requests to a base endpoint
    - Expects a JSON response body
    - Always raises an exception if any HTTP errors are detected.

    >>> session.get('/invalidroute') # Raises HTTPError (404)
    """
    _user_agent = None
    _proxy_configured = None

    def __init__(self, hostname, root_path, verify=True):
        super().__init__()
        self._configure_proxy()
        self.verify = verify
        self.base_url = create_endpoint(hostname, root_path)
        self.headers.update({'User-Agent': self._compose_user_agent()})

    @staticmethod
    def _compose_user_agent():
        if RestSession._user_agent is None:
            script_id = os.path.basename(os.getcwd()) if __file__ == '<input>' else os.path.basename(__file__)

            try:
                os_system = platform.system()
                os_version = platform.release()
            except IOError:
                os_system = "Unknown"
                os_version = "Unknown"

            os_agent = f"{os_system}/{os_version}"
            requests_agent = requests.utils.default_user_agent()
            py_agent = f"{platform.python_implementation()}/{platform.python_version()}"

            RestSession._user_agent = f"{os_agent} {py_agent} {requests_agent} {script_id}"

        return RestSession._user_agent

    @staticmethod
    def _configure_proxy():
        if RestSession._proxy_configured or any(key in os.environ for key in ['PYTHON_DISABLE_FIDDLER', 'http_proxy', 'https_proxy']) or os.name != 'nt':
            RestSession._proxy_configured = True
            return

        if RestSession._windows_process_exists('Fiddler.exe'):
            os.environ['http_proxy'] = os.environ['https_proxy'] = 'http://127.0.0.1:8888'

        RestSession._proxy_configured = True

    @staticmethod
    def _reset_proxy():
        RestSession._proxy_configured = None

    @staticmethod
    def _disable_proxy():
        if not RestSession._proxy_configured:
            os.environ.pop('http_proxy')
            os.environ.pop('https_proxy')
            RestSession._proxy_configured = None

    @staticmethod
    def _windows_process_exists(process_name):
        call = 'TASKLIST', '/FI', 'imagename eq %s' % process_name
        # use builtin check_output right away
        output = subprocess.check_output(call).decode()
        # check in last line for process name
        last_line = output.strip().split('\r\n')[-1]
        # because Fail message could be translated
        return last_line.lower().startswith(process_name.lower())

    def get(self, url, **kwargs):
        return self.json_or_none(self._get_raw(url, **kwargs))

    def post(self, url, data=None, json=None, **kwargs):
        return self.json_or_none(self._post_raw(url, data, json, **kwargs))

    def put(self, url, data=None, **kwargs):
        return self.json_or_none(self._put_raw(url, data, **kwargs))

    def delete(self, url, **kwargs):
        return self.json_or_none(self._delete_raw(url, **kwargs))

    @staticmethod
    def json_or_none(response):
        if response.status_code == 204:
            return None

        return response.json()

    def _get_raw(self, url, **kwargs):
        r = super().get(self.base_url + url, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def _post_raw(self, url, data=None, json=None, **kwargs):
        r = super().post(self.base_url + url, data, json, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def _put_raw(self, url, data=None, **kwargs):
        r = super().put(self.base_url + url, data, verify=self.verify, **kwargs)
        return response_or_raise(r)

    def _delete_raw(self, url, **kwargs):
        r = super().delete(self.base_url + url, verify=self.verify, **kwargs)
        return response_or_raise(r)


class ServiceStackSession(RestSession):
    """
    A requests.Session object for ServiceStack-based REST services.

    AQUARIUS TimeSeries and AQUARIUS WebPortal both use ServiceStack back-ends
    """

    def __init__(self, hostname, root_path, verify=True):
        super().__init__(hostname=hostname, root_path=root_path, verify=verify)
        self.metadata = None

    def send_batch_requests(self, route_or_operation_name, requests, batch_size=100, verb="GET"):
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
        >>> responses = client.publish.send_batch_requests("/GetLocationData", requests)
        :param route_or_operation_name: A parameterized route, like "/locations/{uniqueid}", or the name of operation from the AQTS Metadata page, to perform multiple times.
        :param requests: A list of individual request objects
        :param batch_size: Optional batch size (defaults to 100 requests per batch)
        :param verb: Optional HTTP verb of the operation (defaults to "GET")
        :return: A list of all the responses.
        """
        operation_name = self._get_operation_name(route_or_operation_name, verb)

        url = f"/json/reply/{operation_name}[]"

        # Split the list into batches
        batched_requests = [requests[i:i + batch_size] for i in range(0, len(requests), batch_size)]

        # Get the response batches
        batched_responses = [self.post(url, json=batch, headers={'X-Http-Method-Override': verb}) for batch in batched_requests]

        # Return the flattened response list
        return [response for batch in batched_responses for response in batch]

    def _get_operation_name(self, url, verb='GET'):
        if self.metadata is None:
            # Only fetch this once per session
            self.metadata = {route['operation']: route['name'] for route in [
                item for sublist in [self._get_operation_routes(operation) for operation in
                                     self.get('/types/metadata')['Operations']] for item in sublist]}

        target_route = self._normalize_operation(verb, url)

        if target_route in self.metadata:
            return self.metadata[target_route]

        return url

    def _get_operation_routes(self, operation):
        # ServiceStack 5.xx: metadata['Operations'][i]['Routes'][i]['Path']
        # ServiceStack 4.xx: metadata['Operations'][i]['Request']['Routes'][i]['Path']
        routes = operation['Routes'] if 'Routes' in operation else operation['Request']['Routes']

        return [dict(
            operation=self._normalize_operation(route['Verbs'], route['Path']),
            name=operation['Request']['Name'])
            for route in routes]

    def _normalize_operation(self, verb, url):
        return f"{verb.lower()}/{self._normalize_url(url)}"

    def _normalize_url(self, url):
        # Fold any /{path}/ parameters into /{}/ so any parameter name will match
        # Then lowercase it and strip any trailing slash
        return re.sub('{[^}]+}', '{}', url).rstrip('/').lower()

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


class TimeSeriesSession(ServiceStackSession):
    """
    A requests.Session object for AQUARIUS TimeSeries REST services.
    """

    def __init__(self, hostname, root_path, verify=True):
        super().__init__(hostname=hostname, root_path=root_path, verify=verify)

    def set_session_token(self, token):
        self.headers.update({"X-Authentication-Token": token})


class SingleMetadataResolver:
    """
    Resolves the one metadata item from a sorted list of time-ranged metadata items.

    Each item in the list must have a 'StartTime' (inclusive) and 'EndTime' (exclusive) property.
    """

    def __init__(self, items):
        self.items = items
        self.index = 0
        self.current = self.items[self.index]
        self.index += 1

    def resolve(self, timestamp):
        while True:
            if timestamp < self.current['EndTime']:
                return self.current
            else:
                self.index += 1
                self.current = self.items[self.index]


class timeseries_client:
    """
    A client wrapper for AQUARIUS Time-Series REST API consumption.

    Each of the public REST APIs is exposed as a timeseries_session object:

    publish      => An authenticated session to the /AQUARIUS/Publish/v2 endpoint
    acquisition  => An authenticated session to the /AQUARIUS/Acquisition/v2 endpoint
    provisioning => An authenticated session to the /AQUARIUS/Provisioning/v1 endpoint

    >>> timeseries = timeseries_client('localhost', 'admin', 'admin')
    >>> timeseries.publish.get('/session')
    {'Username': 'admin', 'Locale': 'en', 'Token': 'GWVheAEXYDkJrqKWxFA1vQ2', 'CanConfigureSystem': True, 'IpAddress': '172.16.1.90'}

    Session resources will be automatically cleaned up if used in WITH statement.

    >>> with timeseries_client('localhost', 'admin', 'admin') as timeseries:
    ...   parameters = timeseries.publish.get('/GetParameterList')["Parameters"]
    ...   print (f"There are {len(parameters)} parameters")
    ...
    >>> # The session will be disconnected now, even if an exception was thrown in the body of the WITH statement.
    """

    def __init__(self, hostname, username="admin", password="admin", verify=True):
        # Create the three endpoint sessions
        self.publish = TimeSeriesSession(hostname, "/AQUARIUS/Publish/v2", verify=verify)
        self.acquisition = TimeSeriesSession(hostname, "/AQUARIUS/Acquisition/v2", verify=verify)
        self.provisioning = TimeSeriesSession(hostname, "/AQUARIUS/Provisioning/v1", verify=verify)

        # Authenticate once
        self.connect(username, password)

        # Cache the server version
        version_session = TimeSeriesSession(hostname, "/AQUARIUS/apps/v1", verify=verify)
        self.server_version = ServerVersion(version_session.get('/version')["ApiVersion"])

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception_value, exception_traceback):
        self.disconnect()

    def connect(self, username, password):
        """
        Authenticates the session with AQUARIUS.

        All subsequent requests to any public endpoint will be authenticated using the stored session token.
        """
        token = self.publish._post_raw('/session', json={'Username': username, 'EncryptedPassword': password}).text
        self.publish.set_session_token(token)
        self.acquisition.set_session_token(token)
        self.provisioning.set_session_token(token)

    def disconnect(self):
        """Destroys the authenticated session"""
        self.publish.delete('/session')

    def isVersionLessThan(self, source_version, target_version=None):
        """
        Is the source version strictly less than the target version.

        :param source_version: The source version, in dotted.string notation
        :param target_version: If None, the connected server version is used
        :return: True if the source version is strictly less than the target version
        """
        if target_version is None:
            target_version = self.server_version

        if not isinstance(source_version, ServerVersion):
            source_version = ServerVersion(source_version)

        return source_version < target_version

    def isServerVersionLessThan(self, target_version):
        return self.isVersionLessThan(self.server_version, target_version)

    @staticmethod
    def iso8601(datetime):
        """Formats the datetime object as an ISO8601 timestamp"""
        return pyrfc3339.generate(datetime, microseconds=True)

    @staticmethod
    def datetime(text):
        """Parses the ISO8601 timestamp to a standard python datetime object"""
        if text[10:19] == "T24:00:00":
            # Deal with the quirky end-of-day timestamps from the AQTS Publish API
            # Parse a normalized version and add a day
            return pyrfc3339.parse(text.replace("T24:", "T00:")) + timedelta(days=1)

        return pyrfc3339.parse(text)

    def coerceQueryTime(self, querytime):
        """
        Coerces the timevalue into a best possible query time format.

        Naive datetimes are treated as client-local times.
        Unambiguous datetimes are formated as ISO8601.
        Non datetime objects are left as-is.

        :param querytime: a datetime to
        :return: The coerced text value
        """
        if isinstance(querytime, datetime):
            if querytime.tzinfo is None:
                # Format naive date times as a local time
                return querytime.strftime('%Y-%m-%d %H:%M:%S.%f')
            else:
                # Format unambiguous times as ISO8061
                return self.iso8601(querytime)

        # Otherwise return the value as-is and hope for the best
        return querytime

    def getTimeSeriesUniqueId(self, timeSeriesIdentifier):
        """
        Gets the unique ID of a time-series.

        If the input is not a 'Parameter.Label@Location' identifier, then
        the input is assumed to already be a unique ID and is not modified.

        :param timeSeriesIdentifier: The identifier to lookup
        :return: The unique ID of the series
        """
        parts = timeSeriesIdentifier.split('@')

        if len(parts) < 2:
            return timeSeriesIdentifier

        location = parts[1]

        # Get the descriptions from the location
        try:
            descriptions = self.publish.get(
                '/GetTimeSeriesDescriptionList', params={'LocationIdentifier': location})[
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
            "/GetLocationData", params={'LocationIdentifier': identifier})

    def getLocationUniqueId(self, locationIdentifier):
        """
        Looks up the location unique ID from an identifier.

        If the the input is already a unique ID, no lookup/transformation is performed.

        :param locationIdentifier: The location indentifier to lookup
        :return: The unique ID of the location
        """
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
            })['RatingModelDescriptions']

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
            })['OutputValues']

    def getFieldVisits(self, locationIdentifier, queryFrom=None, queryTo=None, activityType=None):
        fieldVisitDescriptions = self.publish.get(
            "/GetFieldVisitDescriptionList",
            params={
                'LocationIdentifier': locationIdentifier,
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'ActivityType': activityType
            })['FieldVisitDescriptions']

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
                'ExtendedFilters': self.publish.toJSV(extendedFilters)
            })['TimeSeriesDescriptions']

    def getTimeSeriesData(self, timeSeriesIds, queryFrom=None, queryTo=None, outputUnitIds=None, includeGapMarkers=None):
        if isinstance(timeSeriesIds, list):
            timeSeriesIds = [self.getTimeSeriesUniqueId(ts) for ts in timeSeriesIds]
        else:
            timeSeriesIds = self.getTimeSeriesUniqueId(timeSeriesIds)

        return self.publish.get(
            "/GetTimeSeriesData",
            params={
                'TimeSeriesUniqueIds': self.publish.toJSV(timeSeriesIds),
                'TimeSeriesOutputUnitIds': self.publish.toJSV(outputUnitIds),
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'IncludeGapMarkers': includeGapMarkers
            })

    def getTimeSeriesCorrectedData(self, timeSeriesIdentifier, queryFrom=None, queryTo=None, getParts=None, includeGapMarkers=None):
        return self.publish.get(
            "/GetTimeSeriesCorrectedData",
            params={
                'TimeSeriesUniqueId': self.getTimeSeriesUniqueId(timeSeriesIdentifier),
                'QueryFrom': self.coerceQueryTime(queryFrom),
                'QueryTo': self.coerceQueryTime(queryTo),
                'GetParts': getParts,
                'IncludeGapMarkers': includeGapMarkers
            })

    def flattenResponse(self, response):
        """Flattens the metadata in the response, adding point-wise metadata to the 'Points' list"""
        grades = SingleMetadataResolver(response['Grades'])
        approvals = SingleMetadataResolver(response['Approvals'])
        methods = SingleMetadataResolver(response['Methods'])
        gapTolerances = SingleMetadataResolver(response['GapTolerances'])

        for point in response['Points']:
            timestamp = point['Timestamp']
            point['GradeCode'] = grades.resolve(timestamp)
            point['Approval'] = approvals.resolve(timestamp)
            point['Method'] = methods.resolve(timestamp)
            point['GapTolerance'] = gapTolerances.resolve(timestamp)

    def getReportList(self):
        """Gets all the generated reports on the system"""
        return self.publish.get("/GetReportList")['Reports']

    def deleteReport(self, reportUniqueId):
        """Deletes the generated report from the system"""
        self.acquisition.delete(f"/attachments/reports/{reportUniqueId}")

    def uploadExternalReport(self, locationUniqueId, pathToFile, title, deleteDuplicateReports=False):
        """Uploads a file as external report to the given location"""
        if deleteDuplicateReports:
            # Get the current reports
            for r in [r for r in self.getReportList() if r['Title'] == title and not r['IsTransient']]:
                self.deleteReport(r['ReportUniqueId'])

        return self.acquisition.post(f"/locations/{locationUniqueId}/attachments/reports",
                                     params={'Title': title},
                                     files={'file': open(pathToFile, 'rb')})

    def uploadFieldVisit(self, locationUniqueId, pathToFile):
        """Uploads a file as a field visit to the given location"""
        return self.acquisition.post(f"/locations/{locationUniqueId}/visits/upload/plugins",
                                     files={'file': open(pathToFile, 'rb')})

    def createLocation(self, location):
        """ Creates a new location as specified by location dict.
            See API docs for Location JSON/dict struct.
            :param location: dict
            :return: unique ID (str) of created location"""
        response = self.provisioning.post('/locations', json=location)

        return response['UniqueId']

    def getLocationDescriptionList(self, **kwargs):
        locations = self.publish.get('/GetLocationDescriptionList', params=kwargs)
        return locations['LocationDescriptions']

    def getAllLocations(self, **kwargs):
        """
        Gets all the locations in a system.

        :return: A list of Provisioning location objects
        """
        descriptions = self.getLocationDescriptionList(**kwargs)

        return self.provisioning.send_batch_requests(
            '/locations/{Id}',
            [{'LocationUniqueId': loc['UniqueId']} for loc in descriptions])

    def deleteLocation(self, location_identifier_or_unique_id):
        """
        Deletes a location, by location identifier or by unique ID.

        Note: The location needs to be completely empty.
        See https://github.com/AquaticInformatics/examples/tree/master/TimeSeries/PublicApis/SdkExamples/LocationDeleter#locationdeleter for an alternative tool.

        :param location_identifier_or_unique_id: A location identifier or a unique ID string
        """
        location_unique_id = self.getLocationUniqueId(location_identifier_or_unique_id)

        return self.provisioning.delete(f'/locations/{location_unique_id}')

    def createReflectedTimeseries(self, location_identifier_or_unique_id, series):
        """
        Creates a reflected time-series at a location with the given attributes

        :param location_identifier_or_unique_id: A location identifier or a unique ID string
        :param series: dict (see API docs)
        :return: unique ID of create time-series
        """
        location_unique_id = self.getLocationUniqueId(location_identifier_or_unique_id)

        response = self.provisioning.post(
            f'/locations/{location_unique_id}/timeseries/reflected', json=series)

        return response['UniqueId']

    def deleteReflectedTimeseries(self, series_identifier_or_unique_id):
        """
        Deletes a reflected time-series.

        :param series_identifier_or_unique_id: A series identifier or unique ID
        """
        unique_id = self.getTimeSeriesUniqueId(series_identifier_or_unique_id)

        return self.provisioning.delete(f'/timeseries/{unique_id}')

    def appendReflectedPoints(self, series_identifier_or_unique_id, points, start=None, end=None):
        """
        Queues an append request of points to a reflected time-series.

        If the start or end parameters are omitted, the first and last timestamp of the points are assumed.

        :param series_identifier_or_unique_id: A series identifier or unique ID
        :param points: A list of points to append
        :param start: Optional start datetime
        :param end: Optional end datetime
        :return: The append job identifier
        """

        if start is None:
            start = self.datetime(points[0]["Time"])

        if end is None:
            end = self.datetime(points[-1]["Time"]) + timedelta(microseconds=1)

        series_data = {
            'Points': points,
            'TimeRange': {
                'Start': start.isoformat(),
                'End': end.isoformat()
            }}

        unique_id = self.getTimeSeriesUniqueId(series_identifier_or_unique_id)

        return self.acquisition.post(
            f'/timeseries/{unique_id}/reflected', json=series_data)["AppendRequestIdentifier"]

    def appendPoints(self, series_identifier_or_unique_id, points, start=None, end=None):
        """
        Queues an append request of points to a basic time-series.

        If the start and end parameters are omitted, a basic append request is queued. Only new points will be appended.

        When the start and end parameters are provided, an overwrite append request is queued.

        :param series_identifier_or_unique_id: A series identifier or unique ID
        :param points: A list of points to append
        :param start: Optional start datetime
        :param end: Optional end datetime
        :return: The append job identifier
        """

        unique_id = self.getTimeSeriesUniqueId(series_identifier_or_unique_id)
        series_data = {'Points': points}

        if start is None and end is None:
            return self.acquisition.post(
                f'/timeseries/{unique_id}/append', json=series_data)["AppendRequestIdentifier"]

        series_data['TimeRange'] = {
            'Start': start.isoformat(),
            'End': end.isoformat()
        }

        return self.acquisition.post(
            f'/timeseries/{unique_id}/overwriteappend', json=series_data)["AppendRequestIdentifier"]

    def getAppendStatus(self, append_request_identifier):
        """
        Gets the status of a queue append request.

        :param append_request_identifier: The request identifier
        :return: The status of the append request
        """
        return self.acquisition.get(f'/timeseries/appendstatus/{append_request_identifier}')

    def waitForCompletedAppendRequest(self, append_request_identifier, timeout=timedelta(minutes=5)):
        """
        Waits for a queued append request to complete.

        :param append_request_identifier: The request identifier
        :param timeout: Optional timeout parameter. Can be set to None to wait forever
        :return: The completed or failed append request status
        """
        started = datetime.utcnow()
        delay = timedelta(milliseconds=50)

        while True:
            status = self.getAppendStatus(append_request_identifier)

            if status['AppendStatus'] != 'Pending':
                return status

            elapsed = datetime.utcnow() - started

            if timeout and elapsed > timeout:
                return status

            sleep(delay.total_seconds())

            if delay < timedelta(seconds=20):
                delay = delay * 2


class SamplesSession(RestSession):
    """
    A client wrapper for AQUARIUS Samples REST API consumption.

    Obtain a 32-digit API token by browsing to https://myinstance.aqsamples.com/api and following the instructions.

    :param hostname: A thinger
    :param api_token: Another thiner
    :param callbacks: Callbacks for special handling

    >>> samples = SamplesSession("https://myinstance.aqsamples.com", "01234567890123456789012345678901")
    >>>
    >>> # Get all the projects in the system
    >>> projects = samples.get("/v1/projects")["domainObjects"]
    """
    def __init__(self, hostname, api_token, callbacks={}, verify=True):
        super().__init__(hostname=hostname, root_path="/api", verify=verify)
        self.headers.update({"Authorization": f"token {api_token}"})

        # Callbacks must be set before any API requests are issued
        self.callbacks = callbacks
        self.callbacks.setdefault('pagination_warning', self.default_pagination_warning)
        self.callbacks.setdefault('pagination_progress', self.default_pagination_progress)
        self.callbacks.setdefault('on_connected', self.default_on_connected)

        self._connect()

    def _connect(self):
        self.server_version = ServerVersion(self.get('/v1/status')["releaseName"])
        self.authenticated_user = self.get('/v1/usertokens')["user"]

        if self.callbacks['on_connected']:
            self.callbacks['on_connected'](self.base_url, self.server_version, self.authenticated_user)

    def get(self, url, **kwargs):
        json = super().get(url, **kwargs)

        if self.callbacks['pagination_warning'] and json \
                and all(key in json for key in ['totalCount', 'cursor', 'domainObjects']) \
                and json['totalCount'] > len(json['domainObjects']):
            self.callbacks['pagination_warning'](json['domainObjects'], json['totalCount'])

        return json

    @staticmethod
    def default_pagination_warning(page_items, total_count):
        """
        The default callback invoked when a get() response indicates that only a partial result set instead of all the results.

        Set `samples.callbacks['pagination_warning'] = None` to disable the callback.

        :param page_items: A collection of the received items.
        :param total_count: The total item count of all results.
        """
        print(f"WARNING: Only {len(page_items)} of {total_count} items received. Try using the paginated_get() method instead.")

    @staticmethod
    def default_pagination_progress(page_limit, total_count, current_items):
        """
        The default callback invoked when each intermediate page of paginated data is received.

        Set `samples.callbacks['pagination_progress'] = None` to disable the callback.

        :param page_limit: The page size to be used for the next paginated get request.
        :param total_count: The total item count of the entire result set.
        :param current_items: The collection of currently retrieved items.
        """
        print(f"Fetching next page of {page_limit} items ... {len(current_items)/total_count:>3.0%} complete: {len(current_items)} of {total_count} items received.")

    @staticmethod
    def default_on_connected(base_url, server_version, authenticated_user):
        """
        The default callback for connection events.

        Set `samples.callbacks['on_connected'] = None` to disable the callback.

        :param base_url: The root URL of the connected AQUARIUS Samples instance
        :param server_version: The current AQUARIUS Samples server release version
        :param authenticated_user: The authenticated user associated with the API token
        """
        print(f"Connected to {base_url} (v{server_version}) as {authenticated_user['email']}")

    def paginated_get(self, url, early_exit=None, **kwargs):
        """
        Make repeated GET requests from the given URL until all pages have been received.

        :param url: The URL of the get() request for fetching pages of data (Eg. "/v1/samplinglocations")
        :param early_exit: Optional predicate to be applied to each fetched item. When the predicate returns True,
        the paginated sequence will exit early, before all pages have been fetched.
        :param kwargs: Other keyword arguments for the get() request
        :return: An object containing all the retrieved items.
        """
        # Get the supplied parameters, or an empty object if none was supplied
        params = kwargs.pop('params', {})

        next_cursor = None
        total_count = 0
        total_items = []

        while True:
            if next_cursor is not None:
                params['cursor'] = next_cursor

            page_response = self.json_or_none(self._get_raw(url, params=params, **kwargs))

            page_items = page_response['domainObjects']

            # Add this page of items
            total_count = page_response['totalCount']
            total_items.extend(page_items)

            if early_exit is not None and any(early_exit(item) for item in page_items):
                break

            if len(total_items) >= total_count or not any(page_items) or 'cursor' not in page_response:
                break

            if self.callbacks['pagination_progress']:
                page_limit = params['limit'] if 'limit' in params else len(page_items)
                self.callbacks['pagination_progress'](page_limit, total_count, total_items)

            # Use this cursor to fetch the next page
            next_cursor = page_response['cursor']

        return {'totalCount': total_count, 'domainObjects': total_items}
