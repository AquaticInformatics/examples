# Sample python 2.7.x/3.x code to download a range of data from a time-series from AQUARIUS WebPortal

# pip install requests

import argparse
import sys
import csv
import requests
from requests.exceptions import HTTPError
from requests.auth import HTTPBasicAuth


def response_or_raise(response):
    if response.status_code >= 400:
        json = response.json
        if isinstance(json, dict) and 'ResponseStatus' in json:
            http_error_msg = u'%s WebService Error: %s(%s) for url: %s' % (
                response.status_code, response.reason, json['ResponseStatus']['Message'], response.url)
            raise HTTPError(http_error_msg, response=response)

    response.raise_for_status()
    return response


class MyArgumentParser(argparse.ArgumentParser):
    def convert_arg_line_to_args(self, arg_line):
        # Trim each line in file
        arg_line = arg_line.strip()

        # Skip empty lines or comment lines
        if not arg_line or arg_line.startswith("#"):
            return []

        return [arg_line]


parser = MyArgumentParser(
    fromfile_prefix_chars='@',
    description='Download a CSV for a range of time-series data')

parser.add_argument('-s', '--server', dest='server', required=True, help='WebPortal server URL')
parser.add_argument('-u', '--username', dest='username', default='admin')
parser.add_argument('-p', '--password', dest='password', default='admin')
parser.add_argument('-d', '--dataSet', dest='dataSet', required=True, help='The dataset identifier as "Parameter.Label@Location"')
parser.add_argument('--dateRange', dest='dateRange', default=None)
parser.add_argument('--startTime', dest='startTime', default=None)
parser.add_argument('--endTime', dest='endTime', default=None)
parser.add_argument('--unit', dest='unit', default=None, help='Override the time-series unit')
parser.add_argument('--timezone', dest='timezone', default=None, help='Override the time-series UTC offset')
parser.add_argument('--preProcessing', dest='preProcessing', default=None)
parser.add_argument('outfile', nargs='?', type=argparse.FileType('w'), default=sys.stdout)

args = parser.parse_args()

# Build the request parameters
request = dict(
    DataSet=args.dataSet,
    DateRange=args.dateRange,
    StartTime=args.startTime,
    EndTime=args.endTime,
    Unit=args.unit,
    Timezone=args.timezone,
    PreProcessing=args.preProcessing
)

try:
    auth = HTTPBasicAuth(args.username, args.password)

    response = requests.get(args.server+'/api/v1/export/data-set',
                            auth=auth,
                            params=request)

    json = response_or_raise(response).json()

    if args.outfile.name != '<stdout>':
        print("Writing", len(json['points']), "points to", args.outfile.name)

    fieldnames = ['Time', 'Value']

    writer = csv.DictWriter(args.outfile, fieldnames=fieldnames)
    writer.writeheader()

    for p in json['points']:
        writer.writerow({'Time': p['timestamp'], 'Value': p['value']})

except Exception as e:
    sys.exit(e)
