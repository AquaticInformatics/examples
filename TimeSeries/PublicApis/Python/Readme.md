## Consuming AQUARIUS Time-Series data from Python

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FPython)

Requirements: Python 2.7-or-greater

### Required dependencies

The [`timeseries_client.py`](./timeseries_client.py) wrapper class uses the awesome [Requests for Humans](http://docs.python-requests.org/en/master/) package, plus some timezone parsing packages. Install the packages via `pip`.
```bash
$ pip install requests pytz pyrfc3339
```

## Connecting to your AQTS server

Step 1 - Import the API wrapper
```python
# Import the class into your environment
>>> from timeseries_client import timeseries_client
```

Step 2 - Connect to the AQTS server using your credentials.

The `hostname` parameter of the `timeseries_client` constructor supports a number of formats:
- `'myserver'` - Simple DNS name
- `'123.231.132.213'` - IP address (IPv4 or IPv6)
- `'http://myserver'` - HTTP URI
- `'https://myserver'` - HTTPS URI (if you have enabled HTTPS on your AQTS server)

```python
# Connect to the server
>>> timeseries = timeseries_client('myserver', 'myusername', 'mypassword')
```

Now the `timeseries` object represents an authenticated AQTS session.

Step 3 - Make requests from the public API endpoints

The `timeseries` object has `publish`, `acquisition`, and `provisioning` properties that are [`Session objects`](http://docs.python-requests.org/en/master/user/advanced/#session-objects), enabling fluent API requests from those public API endpoints, using the `get()`, `post()`, `put()`, and `delete()` methods.

```python
# Grab the list of parameters from the server
>>> parameters = timeseries.publish.get('/GetParameterList').json()["Parameters"]
```

## Disconnecting from your AQTS server

Your authenticated AQTS session will expire one hour after the last authenticated request made.

It is a recommended (but not required) practice to disconnect from the server when your code is finished making API requests.

Your code can choose to immediately disconnect from AQTS by manually calling the `disconnect` method of the `timeseries` object.

```python
>>> timeseries.disconnect()
# Any further requests made will fail with 401: Unauthorized
```

You can also wrap you code in a python `with` statement, to automatically disconnect when the code block exits (even when an error is raised).

```python
>>> with timeseries_client('localhost', 'admin', 'admin') as timeseries:
...   parameters = timeseries.publish.get('/GetParameterList').json()["Parameters"]
...
>>> # We're logged out now.
```

## Error handling

Any errors contained in the HTTP response will be raised through the `raise_for_status()` method from the `Requests` package.

```python
# Issue a request to an unknown route
>>> r = timeseries.publish.get('/someinvalidroute')
Traceback (most recent call last):
  File "<stdin>", line 1, in <module>
  File "timeseries_client.py", line 39, in get
    return response_or_raise(r)
  File "timeseries_client.py", line 22, in response_or_raise
    response.raise_for_status()
  File "C:\Python27\lib\site-packages\requests\models.py", line 862, in raise_for_status
    raise HTTPError(http_error_msg, response=self)
requests.exceptions.HTTPError: 404 Client Error: Not Found for url: http://myserver/AQUARIUS/Publish/v2/someinvalidroute
```

Your code can use standard python error handling techniques (like  `try ... except ...` blocks) to handle these exceptions as you'd like.

## Formating requests and parsing responses

The `publish`, `acquisition`, and `provisioning` Session objects expose `get()`, `post()`, `put()`, and `delete()` methods for making authenticated HTTP requests and returning response objects.

### The basic request/response pattern for AQTS API operations

The standard API request/response pattern is essentially `response = timeseries.{endpoint}.{verb}('/route', request params...).json()`

- Where `{endpoint}` is one of: `publish`, `acquisition`, or `provisioning`
- Where `{verb}` is one of: `get`, `post`, `put`, or `delete`
- Where `/route` is the route required by the REST operation
- Where `request params...` are any extra request parameters, passed in the URL or in the body. See below for examples.
- Appending the `.json()` method convert the JSON response stream into a python dictionary.

Remember that any HTTP errors will automatically be raised by the wrapper class.
 
See the [Requests quickstart guide](http://docs.python-requests.org/en/master/user/quickstart/#make-a-request) for full details.

#### GET requests require URL parameters

The HTTP spec does not permit GET requests to include a JSON payload.

Any GET request parameters not contained within the route should be specified as a Python dictionary in the `params` argument of the `get()` method. The Requests library will automatically perform [URL encoding](http://www.url-encode-decode.com/) of these query parameters, to ensure that the HTTP request is well-formed.

```python
# Get a list of time-series at a location
>>> payload = {'LocationIdentifier': 'MyLocation'}
>>> list = timeseries.publish.get('/GetTimeSeriesDescriptionList', params=payload).json()["TimeSeriesDescriptions"]
```

#### POST/PUT/DELETE requests use JSON body parameters

Non-GET requests should specify any request parameters as a Python dictionary in the `json` argument. The Requests library will convert the dictionary to a JSON stream and send the request.

```python
# Create a new location
>>> payload = {'LocationIdentifier': 'Loc2', 'LocationName': 'My second location', 'LocationPath': 'All Locations', 'LocationType': 'Hydrology Station'}
>>> location = timeseries.provisioning.post('/locations', json=payload).json()
```
### Refer to your API Reference Guides

You will need to refer to the appropriate AQUARIUS Time-Series API reference guide for any request-specific details. Simply browse to the API endpoint to view the API reference guide.

`http://myserver/AQUARIUS/Publish/v2` will show the Publish API Reference Guide.

Also be sure to read the Common API Reference Guide section on JSON serialization, which describes the expected JSON formats for various data types.

## Working with dates and times

AQTS APIs use ISO 8601 timestamps to represent times unambiguously, either as UTC times or with an explicit offset from UTC.

`yyyy-MM-ddTHH:mm:ss.fffffffZ` or:<br/>`yyyy-MM-ddTHH:mm:ss.fffffff+HH:mm` or:<br/>`yyyy-MM-ddTHH:mm:ss.fffffff-HH:mm`

Up to 7 digits can be specified to represent fractional seconds, yielding a maximum resolution of 100 nanoseconds. Fractional seconds are completely optional. All other fields (including the `T` separating the date and time components) are required.

The wrapper class exposed two helper methods for converting between ISO 8601 timestamp strings and python `datetime` objects.

The `datetime(isoText)` method converts an ISO 8601 string into a python datetime object.

The `iso8601(dt)` method does the reverse, converting a python datetime into an ISO 8601 timestampt string that AQTS can understand.

```python
>>> ts['LastModified']
u'2016-09-12T23:12:37.9704111+00:00'

>>> timeseries.datetime(ts['LastModified'])
datetime.datetime(2016, 9, 12, 23, 12, 37, 970411, tzinfo=<UTC>)

>>> timeseries.iso8601(timeseries.datetime(ts['LastModified']))
'2016-09-12T23:12:37.970411Z'
```

### Beware of naive python datetimes!

Dealing with timestamps unambigously and correctly can be incredibly difficult. (Trust us! It's our job!) Also, see [this](http://infiniteundo.com/post/25326999628/falsehoods-programmers-believe-about-time) if you are curious.

Every major programming language we've dealt with over the years has implemented wrong on their first try. This is true for C, C++, Java, and .NET. It should come as no surprise that Python messed it up as well.

Using python datetime objects without locking them down to an unambiguous offset from UTC is surprising common and is fraught with error.

For instance, did you know that `datetime.utcnow()` does **not** create a a UTC timestamp, as its name implies. Instead it just grabs the current UTC time and **throws away the fact that it came from the UTC timezone**! (sigh)

Python refers to a `datetime` object without a known timezone as a `naive` datetime.

The `iso8601(dt)` helper method will raise an error if it is given a naive datetime.

```python
>>> from datetime import datetime
>>> timeseries.iso8601(datetime.now())
... Boom!
ValueError: naive datetime and accept_naive is False

>>> timeseries.iso8601(datetime.utcnow())
... Boom!
ValueError: naive datetime and accept_naive is False
```

The decision to reject naive datetimes will force your python code to be explicit about the timestamps it provides to AQTS.

To construct correct `datetime` objects, you will need to use the `pytz` library and associate every datetime with a timezone (the simplest being UTC).

```python
>>> from datetime import datetime
>>> import pytz
>>> utc = pytz.UTC

>>> world_water_day = datetime(2017, 3, 22, 12, 0, 0, 0, utc) # Noon UTC
>>> canada_day = datetime(2017, 7, 1, 4, 0, 0, 0, utc) # Midnight, Ottawa

>>> timeseries.iso8601(world_water_day)
'2017-03-22T12:00:00.000000Z'
>>> timeseries.iso8601(canada_day)
'2017-07-01T04:00:00.000000Z'
>>> timeseries.iso8601(timeseries.datetime('2017-07-01T00:00:00-04:00'))
'2017-07-01T04:00:00.000000Z'
```
# Example 1 - Finding the unique ID of a time-series

This example will use the `/GetTimeSeriesDescriptionList` operation from the Publish API to find the unique ID of a time-series.

Most of the time, we refer an AQTS time-series by its identifier string, in `<Parameter>.<Label>@<Location>` format.
But parameter names, labels, and location identifiers can change over time.
Each time-series has a `UniqueId` string property which remains unchanged for the lifetime of the time-series.

Many AQTS APIs which operate on a time-series require this `UniqueId` value as an input.

```python
>>> identifier = "Stage.Working@MyLocation"

# Parse out the location from the time-series identifier string
>>> location = identifier.split('@')[1]
>>> location
'MyLocation'

# Grab all the time-series at the location
>>> descriptions = timeseries.publish.get('/GetTimeSeriesDescriptionList', json={'LocationIdentifier':location}).json()["TimeSeriesDescriptions"]

# Use a '[list comprehension]' to find the exact match
>>> ts = [d for d in descriptions if d['Identifier'] == identifier][0]

# Now grab the UniqueId property
>>> ts['UniqueId']
u'4d5acfc21eb44ab6902dc6547ab82935'
```

# Example 2 - Appending a point to a time-series

This example will build on Example 1 and append 2 points to the time-series using the Acquisition API.

The 2 points to append will be:

Time | Value | Description
---|---|---
`2017-03-22T12:00:00.000000Z` | 24 | 24th anniversary of World Water Day 2017, Noon UTC
`2017-07-01T04:00:00.000000Z` | 150 | 150th anniversay of Canada Day 2017, Midnight Ottawa

```python
>>> from datetime import datetime
>>> import pytz
>>> utc = pytz.UTC

>>> world_water_day = datetime(2017, 3, 22, 12, 0, 0, 0, utc) # Noon UTC
>>> canada_day = datetime(2017, 7, 1, 4, 0, 0, 0, utc) # Midnight, Ottawa

# Create the points array
>>> points = [{'Time': timeseries.iso8601(world_water_day), 'Value': 23},
 {'Time': timeseries.iso8601(canada_day), 'Value': 149}]

# Append these points to the time-series from Example 1
>>> response = timeseries.acquisition.post('/timeseries/'+ts['UniqueId']+'/append', json={'Points': points}).json()
>>> job = response['AppendRequestIdentifier']
>>> job
u'775775'

# The points were queued up for processing by AQTS as append request #775775
# Poll the server for the status of that append job
>>> response = timeseries.acquisition.get('/timeseries/appendstatus/'+job).json()
>>> response
{u'NumberOfPointsAppended': 2, u'NumberOfPointsDeleted': 0, u'AppendStatus': u'Completed'}

# The job status is no longer 'Pending' so we are done.
```


