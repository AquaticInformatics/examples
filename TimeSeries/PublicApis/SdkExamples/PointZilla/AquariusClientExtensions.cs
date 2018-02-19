using System;
using System.Linq;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;

namespace PointZilla
{
    public static class AquariusClientExtensions
    {
        public static Guid GetTimeSeriesUniqueId(this IAquariusClient client, string identifier)
        {
            if (Guid.TryParse(identifier, out var uniqueId))
                return uniqueId;

            var location = TimeSeriesIdentifierParser.ParseLocationIdentifier(identifier);

            var response = client.Publish.Get(new TimeSeriesDescriptionServiceRequest { LocationIdentifier = location });

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == identifier);

            if (timeSeriesDescription == null)
                throw new ExpectedException($"Can't find '{identifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

        public static TimeSeries GetTimeSeriesInfo(this IAquariusClient client, string identifier)
        {
            return client.Provisioning.Get(new GetTimeSeries
            {
                TimeSeriesUniqueId = GetTimeSeriesUniqueId(client, identifier)
            });
        }
    }
}
