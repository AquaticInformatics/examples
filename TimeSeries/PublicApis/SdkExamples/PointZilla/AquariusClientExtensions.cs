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

            var timeSeriesDescription = client.GetTimeSeriesDescription(identifier);

            return timeSeriesDescription.UniqueId;
        }

        public static TimeSeriesDescription GetTimeSeriesDescription(this IAquariusClient client, string identifier)
        {
            var locationIdentifier = TimeSeriesIdentifierParser.ParseLocationIdentifier(identifier);

            var response = client.Publish.Get(new TimeSeriesDescriptionServiceRequest
            {
                LocationIdentifier = client.FindLocationDescription(locationIdentifier).Identifier
            });

            var caseInsensitiveMatches = response
                .TimeSeriesDescriptions
                .Where(t => t.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (!caseInsensitiveMatches.Any())
                throw new ExpectedException($"Can't find '{identifier}' at location '{locationIdentifier}'");

            if (caseInsensitiveMatches.Count == 1)
                return caseInsensitiveMatches.Single();

            var exactMatch = caseInsensitiveMatches
                .FirstOrDefault(t => t.Identifier == identifier);

            if (exactMatch != null)
                return exactMatch;

            throw new ExpectedException($"{caseInsensitiveMatches.Count} ambiguous matches for '{identifier}': {string.Join(", ", caseInsensitiveMatches.Select(t => t.Identifier))}");
        }

        public static TimeSeries GetTimeSeriesInfo(this IAquariusClient client, string identifier)
        {
            return client.Provisioning.Get(new GetTimeSeries
            {
                TimeSeriesUniqueId = GetTimeSeriesUniqueId(client, identifier)
            });
        }

        public static LocationDescription FindLocationDescription(this IAquariusClient client, string locationIdentifier)
        {
            if (client.TryGetLocationDescription(locationIdentifier, out var locationDescription))
                return locationDescription;

            throw new ExpectedException($"Location '{locationIdentifier}' does not exist in the system.");
        }

        public static bool TryGetLocationDescription(this IAquariusClient client, string locationIdentifier, out LocationDescription locationDescription)
        {
            var locationDescriptions = client.Publish.Get(new LocationDescriptionListServiceRequest
            {
                LocationIdentifier = locationIdentifier
            }).LocationDescriptions;

            if (locationDescriptions.Count == 1)
            {
                locationDescription = locationDescriptions
                    .First();

                return true;
            }

            locationDescription = default;

            if (!locationDescriptions.Any())
                return false;

            throw new ExpectedException($"{locationDescriptions.Count} ambiguous location identifiers matched '{locationIdentifier}': {string.Join(", ", locationDescriptions.Select(l => l.Identifier))}");
        }
    }
}
