## Consuming AQUARIUS Time-Series data from Python

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FPython)

Requirements: Python 3.7-or-greater

Are you stuck in the Python 2.7 past? [This older wrapper version](https://github.com/AquaticInformatics/examples/blob/ccd0f270d432c369e3b29b782a5db47cae251bea/TimeSeries/PublicApis/Python/timeseries_client.py) should still work, but seriously, [Python 2.x is dead](https://pythonclock.org/). Join the 21st century. ([2to3](https://docs.python.org/3/library/2to3.html) is your friend for quickly bringing your old code into the new world.) 

### Required dependencies

The [`timeseries_client.py`](./timeseries_client.py) wrapper class uses the awesome [Requests for Humans](http://docs.python-requests.org/en/master/) package, plus some timezone parsing packages. Install the packages via `pip`.
```bash
$ pip install requests pytz pyrfc3339
```

## Revision History

- 2021-Sep-01 - Fairly big internal refactoring, with minimal external changes.
    - Dropped Python 2.x support
    - Added an improved User Agent header to all requests
    - Fixed `send_batch_requests()` for AQTS 2021.1+ while still working with AQTS 2020.4-and-older
    - Added AQUARIUS Samples support
- 2020-Sep-03 - Eliminated the `.json()` ceremony around each API operation
- 2019-Dec-13 - Added field visit upload helper method
- 2018-Dec-10 - Added some helper methods
- 2017-Jun-08 - First release

Only major changes are listed above. See the [change log](https://github.com/AquaticInformatics/examples/commits/master/TimeSeries/PublicApis/Python/timeseries_client.py) for the detailed history of the Python API wrapper.

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
- `'https://myinstance.aquaticinformatics.net'` - HTTPS URI for an AQUARIUS Cloud instance

```python
# Connect to the server
>>> timeseries = timeseries_client('myserver', 'myusername', 'mypassword')
```

Now the `timeseries` object represents an authenticated AQTS session.

Note: AQTS API access from python requires a credentialed account. You cannot use an ActiveDirectory or OpenIDConnect account for API access.

Step 3 - Make requests from the public API endpoints

The `timeseries` object has `publish`, `acquisition`, and `provisioning` properties that are [`Session objects`](http://docs.python-requests.org/en/master/user/advanced/#session-objects), enabling fluent API requests from those public API endpoints, using the `get()`, `post()`, `put()`, and `delete()` methods.

```python
# Grab the list of parameters from the server
>>> parameters = timeseries.publish.get('/GetParameterList')["Parameters"]
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
...   parameters = timeseries.publish.get('/GetParameterList')["Parameters"]
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

## Formatting requests and parsing responses

The `publish`, `acquisition`, and `provisioning` Session objects expose `get()`, `post()`, `put()`, and `delete()` methods for making authenticated HTTP requests and returning response objects.

### The basic request/response pattern for AQTS API operations

The standard API request/response pattern is essentially `response = timeseries.{endpoint}.{verb}('/route', request params...)`

- Where `{endpoint}` is one of: `publish`, `acquisition`, or `provisioning`
- Where `{verb}` is one of: `get`, `post`, `put`, or `delete`
- Where `/route` is the route required by the REST operation
- Where `request params...` are any extra request parameters, passed in the URL or in the body. See below for examples.
- The returned JSON response is automatically converted into a python dictionary, or `None` if a `204 (No Content)` response is received.

Remember that any HTTP errors will automatically be raised by the wrapper class.
 
See the [Requests quickstart guide](http://docs.python-requests.org/en/master/user/quickstart/#make-a-request) for full details.

#### GET requests require URL parameters

The HTTP spec does not permit GET requests to include a JSON payload.

Any GET request parameters not contained within the route should be specified as a Python dictionary in the `params` argument of the `get()` method. The Requests library will automatically perform [URL encoding](http://www.url-encode-decode.com/) of these query parameters, to ensure that the HTTP request is well-formed.

```python
# Get a list of time-series at a location
>>> payload = {'LocationIdentifier': 'MyLocation'}
>>> list = timeseries.publish.get('/GetTimeSeriesDescriptionList', params=payload)["TimeSeriesDescriptions"]
```

#### POST/PUT/DELETE requests use JSON body parameters

Non-GET requests should specify any request parameters as a Python dictionary in the `json` argument. The Requests library will convert the dictionary to a JSON stream and send the request.

```python
# Create a new location
>>> payload = {'LocationIdentifier': 'Loc2', 'LocationName': 'My second location', 'LocationPath': 'All Locations', 'LocationType': 'Hydrology Station'}
>>> location = timeseries.provisioning.post('/locations', json=payload)
```

```python
# Change the display name of an existing parameter
# First fetch all the parameters in the system
>>> parameters = timeseries.provisioning.get('/parameters')['Results']

# Find the Stage parameter by parameter ID
>>> stage = next(p for p in parameters if p['ParameterId'] == 'HG')

# Change the identifier
>>> stage['Identifier'] = 'Stagey Thing'

# Issue the PUT request with the modified object
>>> timeseries.provisioning.put('/parameters/'+stage['UniqueId'], json=stage)
```

### Debugging your API scripts (Hint: use Fiddler to confirm the API request/response traffic!)

_"The API didn't work"_ - Usually that isn't true.

TL;DR - Run a web proxy like Fiddler in the background, while your Python script runs. If something fails unexpectedly, look at the traffic for a big red line indicating a failed API operation.

REST APIs rarely actually fail. If your HTTP request receives an HTTP response,
then the API operation has technically "worked as designed".

Most of the time, an unexpected server response raises an HTTP error in your Python script because:
- the request might be incorrect, causing the operation to respond with a 4xx response status.
- a server-side error might have been encountered, causing the operation to respond with a 5xx response status.

But receiving a 4xx or 5xx status code (instead of the expected 2xx status code for successful operations) is still a working API.

The more common problem is that your script isn't handling errors in a meaningful way, but it should. See the [Error Handling](#error-handling) section for details.

Usually the true source of the error can be quickly understood by examining the HTTP request and its HTTP response together. There is usually a clue somewhere in those two items which explains why things are going sideways.

The API wrapper includes automatic support for the excellent-and-free [Fiddler](https://www.telerik.com/fiddler/fiddler-classic) web debugging proxy for Windows.

If your Python script is run on a Windows system while Fiddler is running in the background, all the API requests from your script will be routed through Fiddler.

When an API request fails, the server will respond with a 4xx or 5xx status code, which shows as a red session in the [Fiddler capture window](https://docs.telerik.com/fiddler/observe-traffic/tasks/examinewebtraffic).

#### Using Fiddler for HTTPS connections

Fiddler does have support for HTTPS traffic capture, but it [needs to be expilictly enabled](https://docs.telerik.com/fiddler/configure-fiddler/tasks/decrypthttps) on your development system, so that it can install a [self-signed certificate](https://docs.telerik.com/fiddler/configure-fiddler/tasks/trustfiddlerrootcert) to intercept and re-encrypt the traffic.

If your target AQUARIUS server has HTTPS enabled (it should, and all our AQUARIUS Cloud instances only accept HTTPS requests), then you will need to do two things to allow Fiddler to capture your script's API traffic:
- Configure Fiddler to [capture HTTPS traffic and trust the Fiddler Root certificate](https://docs.telerik.com/fiddler/configure-fiddler/tasks/trustfiddlerrootcert).
- Specify the `verify=False` parameter when connecting, to tell Python to ignore certificate validation errors

```python
# Connect to the HTTPS server, allowing for Fiddler traffic interception
>>> timeseries = timeseries_client('https://myserver.aquaticinformatics.net', 'myusername', 'mypassword', verify=False)
```

#### Disabling automatic Fiddler traffic routing

There is a small one-time price paid when your script is run, since the API wrapper attempts to detect if a `Fiddler.exe` process is running on your system.

If you don't want your script's traffic to be routed through Fiddler (or even try to detect if Fiddler is running),
you can do any of the following before connecting to the server:
- Set the `PYTHON_DISABLE_FIDDLER` environment variable to any value
- Set the `http_proxy` or `https_proxy` environment variables to any string value, including and empty string `""`

```python
import os

# Disable any automatic Fiddler capture
os.environ['PYTHON_DISABLE_FIDDLER'] = True

# Now connect to your server
timeseries = timeseries_client('https://myserver.aquaticinformatics.net', 'myusername', 'mypassword')

# ... make API requests ...
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
'2016-09-12T23:12:37.9704111+00:00'

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

## Helper methods in the `timeseries_client.py` wrapper

The `timeseries_client.py`  wrapper exposes a few helper methods to make your python integrations simpler.

- `iso8601(datetime)` - Converts a Python datetime into an ISO 8601 text string
- `datetime(text)` - Converts an ISO8601 text string into a python datetime
- `getTimeSeriesUniqueId(timeSeriesIdentifier)` - Gets the unique ID from a text identifier
    - Will raise a `ModelNotFoundException` if the location or time-series does not exist
- `getLocationData(locationIdentifier)` - Gets the attributes of a location
- `getReportList()` - Gets the list of generated reports on the system
- `deleteReport(reportUniqueId)` - Deletes a specific generated report
- `uploadExternalReport(locationUniqueId, pathToFile, title)` - Uploads a file as an external report

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
>>> descriptions = timeseries.publish.get('/GetTimeSeriesDescriptionList', json={'LocationIdentifier':location})["TimeSeriesDescriptions"]

# Use a '[list comprehension]' to find the exact match
>>> ts = [d for d in descriptions if d['Identifier'] == identifier][0]

# Now grab the UniqueId property
>>> tsUniqueId = ts['UniqueId']
>>> tsUniqueId
'4d5acfc21eb44ab6902dc6547ab82935'
```

Since this operation is common enough, the wrapper includes a `getTimeSeriesUniqueId()` method which does this work for you.

```python
# Use the helper method to do all the work
>>> tsUniqueId = timeseries.getTimeSeriesUniqueId("Stage.Working@MyLocation")
>>> tsUniqueId
'4d5acfc21eb44ab6902dc6547ab82935'
```

The `getTimeSeriesUniqueId()` method is also smart enough to recognize unique IDs as input, so if your code passes in a unique ID, the method just returns it as-is. This gives your code a bit more flexibility in the types of arguments it can accept for your scripting tasks.

```python
>>> tsUniqueId = timeseries.getTimeSeriesUniqueId("4d5acfc21eb44ab6902dc6547ab82935")
>>> tsUniqueId
'4d5acfc21eb44ab6902dc6547ab82935'
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
>>> response = timeseries.acquisition.post('/timeseries/'+ts['UniqueId']+'/append', json={'Points': points})
>>> job = response['AppendRequestIdentifier']
>>> job
'775775'

# The points were queued up for processing by AQTS as append request #775775
# Poll the server for the status of that append job
>>> response = timeseries.acquisition.get('/timeseries/appendstatus/'+job)
>>> response
{'NumberOfPointsAppended': 2, 'NumberOfPointsDeleted': 0, 'AppendStatus': 'Completed'}

# The job status is no longer 'Pending' so we are done.
```

# Example 3 - Fetching data from many locations

This example demonstrates how to fetch data about all the locations in your system.

Some data is not fully available in a single API call.
One common use case is to find details of all the locations in your system.

Your code will need to make multiple requests, 1 + #NumberOfLocations:
- An initial `GET /AQUARIUS/Publish/v2/GetLocationDescriptionList` request, to fetch the known location identifiers, unique IDs, names, and folder properties.
- Multiple `GET /AQUARIUS/Provisioning/v1/locations/{uniqueId}` requests
- Or multiple `GET /AQUARIUS/Publish/v2/GetLocationData?LocationIdentifer={locationIdentifier}` requests.

That `NumberOfLocations` might be very large for your system. Maybe thousands, or tens of thousands of locations.

(Using the Publish API is a bit slower to retrieve location details than Provisioning API, since the Publish API response also includes location names, datums, and reference point information, which take a bit more time to fetch from the database.)

You could make those multiple requests in a loop, one at a time:

```python
# Fetch the initial location description list
locationDescriptions = timeseries.publish.get('/GetLocationDescriptionList')['LocationDescriptions']

# Fetch each location separately 
locations = [timeseries.provisioning.get('/locations/'+loc['UniqueId']) for loc in locationDescriptions]
```

That loop may take 5-6 minutes to fetch 20K locations, depending mainly on:
- the speed of the network between your python code and your AQUARIUS application server
- the speed of the network between your app server and your database server
- the speed of your database server

This wrapper also includes a [`send_batch_requests()`](timeseries_client.py#L113) helper method, to make repeated API requests in small batches, which can often double your perceived throughput.

The `send_batch_requests(url, requests)` method takes a URL pattern and a collection of request objects to fetch.
- `url` is a the url you would normally use in the `get()` method. If the route contains parameters, enclose them in `{curlyBraces}`.
- `requests` is a collection of request objects
- `batch_size` is an optional parameter, which defaults to 100 requests per batch.
- `verb` is an optional parameter, which defaults to `GET`.

The same batch-fetch of Provisioning information may take only 2-or-3 minutes:

```python
# Fetch the initial location description list
locationDescriptions = timeseries.publish.get('/GetLocationDescriptionList')['LocationDescriptions']

# Fetch the full location information from the Provisioning API (faster)
locations = timeseries.provisioning.send_batch_requests('/locations/{Id}', [{'LocationUniqueId': loc['UniqueId']} for loc in locationDescriptions])

# Or, alternatively fetch the full location information from the Publish API, but this is slower
locationData = timeseries.publish.send_batch_requests('/GetLocationData', [{'LocationIdentifier': loc['Identifier']} for loc in locationDescriptions])
```

Either approach is fine, but sometimes the batch-fetch might save a few minutes in a long-running script.
