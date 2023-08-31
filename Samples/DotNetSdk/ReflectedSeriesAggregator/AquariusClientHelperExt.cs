using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using MonitoringMethod = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.MonitoringMethod;

namespace ReflectedSeriesAggregator
{
    internal static class AquariusClientHelperExt
    {
        public static readonly Duration NoGaps = Duration.FromTicks(long.MaxValue);

        public class GetOrCreateTimeSeriesResponse
        {
            public bool IsNew { get; set; }
            public Guid UniqueId { get; set; }
            public string SeriesIdentifier { get; set; }
        }

        static public GetOrCreateTimeSeriesResponse GetOrCreateTimeSeries(this IAquariusClient client,
            LocationDescription location,
            Parameter parameter,
            string label,
            string methodCode,
            bool publish,
            Duration gapTolerance,
            SeriesCreateType seriesCreateType = SeriesCreateType.Reflected,
            Dictionary<string, string> extendedAttributes = null)
        {
            var timeSeriesDescriptions = GetTimeSeriesDescriptions(client, location.Identifier, parameter.Identifier);

            var timeSeriesDescription = timeSeriesDescriptions.FirstOrDefault(ts => ts.Label == label);
            if (timeSeriesDescription != null)
                return new GetOrCreateTimeSeriesResponse { IsNew = false, UniqueId = timeSeriesDescription.UniqueId, SeriesIdentifier = timeSeriesDescription.Identifier };

            Dictionary<string, string> remappedExtendedAttibutesByUniqueId = new Dictionary<string, string>();
            if (extendedAttributes != null)
            {
                var timeseriesExtendedAttributes = new GetExtendedAttributes()
                {
                    Applicability = new List<ExtendedAttributeApplicability>()
                    {
                        ExtendedAttributeApplicability.AppliesToTimeSeries
                    }
                };

                // Remap extended attributes
                var aqExtendedAttributes = client.Provisioning.Get(timeseriesExtendedAttributes).Results;
                foreach (var userExtendedAttribute in extendedAttributes)
                {
                    var aqExtendedAttribute = aqExtendedAttributes.Where(a => a.Key.Equals(userExtendedAttribute.Key, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                    if (aqExtendedAttribute != null)
                        remappedExtendedAttibutesByUniqueId.Add(aqExtendedAttribute.UniqueId.ToString("N"), userExtendedAttribute.Value);
                }
            }

            var timeSeries = seriesCreateType == SeriesCreateType.Basic
                ? client.CreateBasicTimeSeries(location.UniqueId, label, parameter, Offset.FromMilliseconds((int)location.UtcOffset * 60 * 60 * 1000), methodCode, publish, gapTolerance, remappedExtendedAttibutesByUniqueId)
                : client.CreateReflectedTimeSeries(location.UniqueId, label, parameter, Offset.FromMilliseconds((int)location.UtcOffset * 60 * 60 * 1000), methodCode, publish, gapTolerance, remappedExtendedAttibutesByUniqueId);

            return new GetOrCreateTimeSeriesResponse { IsNew = true, UniqueId = timeSeries.UniqueId, SeriesIdentifier = timeSeries.Identifier };
        }

        private static Aquarius.TimeSeries.Client.ServiceModels.Provisioning.TimeSeries CreateReflectedTimeSeries(
          this IAquariusClient client,
          Guid locationUniqueId,
          string label,
          Parameter parameter,
          Offset utcOffset,
          string methodCode,
          bool publish,
          Duration gapTolerance,
          Dictionary<string, string> extendedAttributes)
        {
            return client.Provisioning.Post(
                new PostReflectedTimeSeries
                {
                    LocationUniqueId = locationUniqueId,
                    Parameter = parameter.ParameterId,
                    Label = label,
                    Unit = parameter.UnitIdentifier,
                    InterpolationType = parameter.InterpolationType,
                    Method = methodCode,
                    UtcOffset = utcOffset,
                    Publish = publish,
                    GapTolerance = gapTolerance,
                    ExtendedAttributeValues = extendedAttributes?.Select(
                        x => new ExtendedAttributeValue { UniqueId = x.Key, Value = x.Value }).ToList()
                });
        }


        private static Aquarius.TimeSeries.Client.ServiceModels.Provisioning.TimeSeries CreateBasicTimeSeries(
            this IAquariusClient client,
            Guid locationUniqueId,
              string label,
              Parameter parameter,
              Offset utcOffset,
              string methodCode,
              bool publish,
              Duration gapTolerance,
              Dictionary<string, string> extendedAttributes)
        {
            return client.Provisioning.Post(
                new PostBasicTimeSeries
                {
                    LocationUniqueId = locationUniqueId,
                    Parameter = parameter.ParameterId,
                    Label = label,
                    Unit = parameter.UnitIdentifier,
                    InterpolationType = parameter.InterpolationType,
                    Method = methodCode,
                    Publish = publish,
                    UtcOffset = utcOffset,
                    GapTolerance = gapTolerance,
                    ExtendedAttributeValues = extendedAttributes?.Select(
                        x => new ExtendedAttributeValue { UniqueId = x.Key, Value = x.Value }).ToList()
                });
        }

        public static List<LocationDescription> GetLocationDescriptions(this IAquariusClient client, List<string> tags) =>
            (client.Publish.Get(new LocationDescriptionListServiceRequest
            {
                TagKeys = tags,
            })).LocationDescriptions;


        public static List<LocationDescription> GetLocationDescriptions(this IAquariusClient client, string tag = null) => GetLocationDescriptions(client, new List<string> { tag });

        public static List<TimeSeriesDescription> GetTimeSeriesDescriptions(this IAquariusClient client, string locationIdentifier,
            string parameter = null) =>
            client.Publish.Get(new TimeSeriesDescriptionServiceRequest
            {
                LocationIdentifier = locationIdentifier,
                Parameter = parameter
            }).TimeSeriesDescriptions;

        public static List<TimeSeriesDescription> GetTimeSeriesDescriptionsByTag(this IAquariusClient client, string tag)
        {
            List<TimeSeriesDescription> timeSeriesDescriptions = new List<TimeSeriesDescription>();
            var locations = client.GetLocationDescriptions(tag);
            foreach (var location in locations)
                timeSeriesDescriptions.AddRange(client.GetTimeSeriesDescriptions(location.Identifier));

            return timeSeriesDescriptions;
        }

        public static TimeSeriesDescription GetTimeSeries(this IAquariusClient client, Guid timeSeriesUniqueId)
        {
            var ts = client.Provisioning.Get(new GetTimeSeries
            {
                TimeSeriesUniqueId = timeSeriesUniqueId
            });

            return GetTimeSeriesDescriptions(client, ts.LocationIdentifier, ts.Parameter).FirstOrDefault(tsd => tsd.Label == ts.Label);
        }

        private static TimeSeriesType ParseTimeSeriesType(TimeSeriesDescription timeSeriesDescription)
        {
            return
                (TimeSeriesType)
                Enum.Parse(typeof(TimeSeriesType), timeSeriesDescription.TimeSeriesType, true);
        }

        public static List<Parameter> GetParameters(this IAquariusClient client) =>
             (client.Provisioning.Get(new GetParameters())).Results;

        public static List<MonitoringMethod> GetMonitoringMethods(this IAquariusClient client) => client.Provisioning.Get(
                    new GetMonitoringMethods()).Results;
    }

    public enum SeriesCreateType
    {
        Basic,
        Reflected
    }
}
