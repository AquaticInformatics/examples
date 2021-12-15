import argparse
import logging
from timeseries_client import timeseries_client
from requests.exceptions import HTTPError

log = logging.getLogger(__name__)


def main():
    logging.basicConfig(format='%(asctime)s [%(levelname)s] %(message)s', level=logging.INFO)

    # Create the argument parser with defaults
    parser = argparse.ArgumentParser(formatter_class=argparse.ArgumentDefaultsHelpFormatter)

    parser.add_argument('--server', help='The AQTS app server', default='localhost')
    parser.add_argument('--username', help='The AQTS username', default='admin')
    parser.add_argument('--password', help='The AQTS password', default='admin')
    parser.add_argument('--location', help='Filter to this one location. Defaults to all locations if omitted.')
    parser.add_argument('--locationFolder', help='Filter to this one location folder. Defaults to all locations if omitted.')
    parser.add_argument('--queryFrom', help='Filter results to approvals after this date/time. Defaults to the start of record.')
    parser.add_argument('--queryTo', help='Filter results to approvals before this date/time. Defaults to the start of record.')
    parser.add_argument('--reportFilename', help='The name of generated report file in each location', default='LocationApprovalReport')
    parser.add_argument('--keepDuplicates', help='When set, duplicate report files will be kept at each location', action='store_true', default=False)
    parser.add_argument('--tags', help='A comma-separated list of tag:value pairs to apply to the uploaded report.')

    args = parser.parse_args()

    if args.location and args.locationFolder:
        raise Exception(f'You should only set one of {args.location=} or {args.locationFolder=}')
    
    if not args.server or not args.username or not args.password:
        raise Exception(f'All three of {args.server=}, {args.username=}, and {args.password} must be set.')

    # Connect to AQTS
    with timeseries_client(args.server, args.username, args.password) as timeseries:
        tags = parse_tags(timeseries, args)

        locations = timeseries.publish.get('/GetLocationDescriptionList', params={
            'LocationIdentifier': args.location,
            'LocationFolder': args.locationFolder})['LocationDescriptions']

        log.info(f'There are {len(locations)} locations found.')

        query_from = timeseries.coerceQueryTime(args.queryFrom)
        query_to = timeseries.coerceQueryTime(args.queryTo)

        for location in sorted(locations, key=lambda loc: loc['Identifier']):
            examine_location(timeseries, location, query_from, query_to, tags, args.reportFilename)


def parse_tags(timeseries, args):
    """Returns the tags selectors to set for any uploaded documents"""
    if not args.tags:
        return None

    attachment_tags = {tag['Key']: tag for tag in timeseries.provisioning.get('/tags')['Results'] if tag['AppliesToAttachments']}

    tag_selectors = []

    for pair in [[s.strip() for s in pair.split(':', 1)] for pair in args.tags.split(',')]:
        key = pair[0]

        if key not in attachment_tags:
            raise NameError(f"'{key}' is not a valid attachment tag key.")

        tag = attachment_tags[key]

        tag_selector = {'UniqueId': tag['UniqueId']}

        if len(pair) == 2:
            tag_selector['Value'] = pair[1]

        tag_selectors.append(tag_selector)

    return tag_selectors


def examine_location(timeseries, location, query_from, query_to, tags, report_filename):
    """Examine the approvals of one location and upload the report"""
    location_identifier = location['Identifier']
    log.info(f'{location_identifier=}: Loading ...')

    rating_models = timeseries.getRatings(locationIdentifier=location_identifier)
    log.info(f'{location_identifier}: {len(rating_models)} rating models.')

    for model in rating_models:
        model_identifier = model['Identifier']
        try:
            curves = timeseries.publish.get('/GetRatingCurveList', params={
                'RatingModelIdentifier': model_identifier,
                'QueryFrom': query_from,
                'QueryTo': query_to
            })['RatingCurves']
            log.info(f'{model_identifier}: {len(curves)} curves.')
        except HTTPError as e:
            log.error(f'{model_identifier}: {e}')

    visits = timeseries.publish.get('/GetFieldVisitDataByLocation', params={
        'LocationIdentifier': location_identifier,
    })['FieldVisitData']
    log.info(f'{location_identifier}: {len(visits)} field visits.')

    series = timeseries.getTimeSeriesDescriptions(locationIdentifier=location_identifier)
    log.info(f'{location_identifier}: {len(series)} time-series')

    for ts in series:
        ts_identifier = ts['Identifier']
        data = timeseries.getTimeSeriesCorrectedData(timeSeriesIdentifier=ts_identifier,
                                                     queryFrom=query_from, queryTo=query_to, getParts='MetadataOnly')
        log.info(f'{ts_identifier}: {len(data["Approvals"])} approval regions.')

    # TODO: Format this as an actual CSV or Excel doc
    report_content = f'''Approval report
===============
Location: {location['Identifier']} ({location['Name']})
Rating Models: {len(rating_models)}
Visits:{len(visits)} 
Time-Series: {len(series)}
'''

    attachment = timeseries.acquisition.post(f'/locations/{location["UniqueId"]}/attachments',
                                             params={'Tags': timeseries.acquisition.toJSV(tags)},
                                             files={'file': (report_filename, report_content, 'text/plain')})
    log.info(f'{location_identifier}: Uploaded "{report_filename}" to {attachment["Url"]} with {tags=}')


if __name__ == '__main__':
    main()
