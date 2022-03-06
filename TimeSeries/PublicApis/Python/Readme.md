## Consuming AQUARIUS Time-Series data from Python

[**Download** this project folder](https://minhaskamal.github.io/DownGit/#/home?url=https:%2F%2Fgithub.com%2FAquaticInformatics%2FExamples%2Ftree%2Fmaster%2FTimeSeries%2FPublicApis%2FPython)

Requirements: Python 3.7-or-greater

Are you stuck in the Python 2.7 past? [This older wrapper version](https://github.com/AquaticInformatics/examples/blob/ccd0f270d432c369e3b29b782a5db47cae251bea/TimeSeries/PublicApis/Python/timeseries_client.py) should still work, but seriously, [Python 2.x is dead](https://pythonclock.org/). Join the 21st century. ([2to3](https://docs.python.org/3/library/2to3.html) is your friend for quickly bringing your old code into the new world.) 

### Required dependencies

The [`timeseries_client.py`](./timeseries_client.py) wrapper class uses the awesome [Requests for Humans](http://docs.python-requests.org/en/master/) package, plus some timezone parsing packages. Install the packages via `pip`.
```bash
$ pip install requests pytz pyrfc3339
```

## Simple Hello-world for AQTS

```python
from timeseries_client import timeseries_client

with timeseries_client('https://myserver', 'myusername', 'mypassword') as client:
    locations = client.publish.get('/GetLocationDescriptionList')['LocationDescriptions']
    print(f'{client.publish.base_url} ({client.server_version}) has {len(locations)} locations.')
```
## Detailed documentation is on the wiki page

See this repo's [Python wiki page](https://github.com/AquaticInformatics/examples/wiki/Python-integration) for more detailed examples.
