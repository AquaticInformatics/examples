using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using NodaTime;
using ServiceStack.Logging;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;

namespace PointZilla
{
    public class TimeSeriesCreator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }
        public IAquariusClient Client { get; set; }

        public void CreateMissingTimeSeries(string timeSeriesIdentifier)
        {
            var locationIdentifier = TimeSeriesIdentifierParser.ParseLocationIdentifier(timeSeriesIdentifier);

            var location = GetOrCreateLocation(locationIdentifier);

            GetOrCreateTimeSeries(location, timeSeriesIdentifier);
        }

        private Location GetOrCreateLocation(string locationIdentifier)
        {
            var locationDescription = Client.Publish.Get(new LocationDescriptionListServiceRequest
                    {LocationIdentifier = locationIdentifier})
                .LocationDescriptions
                .SingleOrDefault();

            return locationDescription == null
                ? CreateLocation(locationIdentifier)
                : Client.Provisioning.Get(new GetLocation {LocationUniqueId = locationDescription.UniqueId});
        }

        private Location CreateLocation(string locationIdentifier)
        {
            var locationTypes = Client.Provisioning.Get(new GetLocationTypes())
                .Results;

            var locationType = locationTypes.FirstOrDefault(lt => lt.TypeName.Contains("Hydro"))
                               ?? locationTypes.First();

            var locationFolder = Client.Provisioning.Get(new GetLocationFolders())
                .Results
                .First(lf => !lf.ParentLocationFolderUniqueId.HasValue);

            var request = new PostLocation
            {
                LocationType = locationType.TypeName,
                LocationIdentifier = locationIdentifier,
                LocationName = $"PointZilla-{locationIdentifier}",
                Description = "Dummy location created by PointZilla",
                LocationPath = locationFolder.LocationFolderPath,
                UtcOffset = Context.UtcOffset ?? Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks),
                ExtendedAttributeValues = MergeExtendedAttributesWithMandatoryExtendedAttributes(locationType.ExtendedAttributeFields).ToList()
            };

            Log.Info($"Creating location '{locationIdentifier}' ...");

            return Client.Provisioning.Post(request);
        }

        private void GetOrCreateTimeSeries(Location location, string timeSeriesIdentifier)
        {
            var existingTimeSeries = Client.Provisioning.Get(new GetLocationTimeSeries {LocationUniqueId = location.UniqueId})
                .Results
                .FirstOrDefault(ts => ts.Identifier == timeSeriesIdentifier);

            if (existingTimeSeries != null)
                return;

            var timeSeriesExtendedAttributes = Client.Provisioning.Get(new GetTimeSeriesExtendedAttributes())
                .Results;

            var timeSeriesInfo = TimeSeriesIdentifierParser.ParseExtendedIdentifier(timeSeriesIdentifier);

            var parameter = Client.Provisioning.Get(new GetParameters())
                .Results
                .FirstOrDefault(p => p.Identifier.Equals(timeSeriesInfo.Parameter, StringComparison.InvariantCultureIgnoreCase));

            if (parameter == null)
                throw new ExpectedException($"Parameter '{timeSeriesInfo.Parameter}' does not exist in the system.");

            var interpolationType = Context.InterpolationType ?? parameter.InterpolationType;

            var gapTolerance = InterpolationTypesWithNoGaps.Contains(interpolationType)
                ? DurationExtensions.MaxGapDuration
                : Context.GapTolerance;

            var defaultMonitoringMethod = Client.Provisioning.Get(new GetMonitoringMethods())
                .Results
                .Single(monitoringMethod => monitoringMethod.ParameterUniqueId == Guid.Empty);

            PostBasicTimeSeries basicTimeSeries = null;
            PostReflectedTimeSeries reflectedTimeSeries = null;
            TimeSeriesBase request;

            if (Context.CreateMode == CreateMode.Reflected)
            {
                reflectedTimeSeries = new PostReflectedTimeSeries {GapTolerance = gapTolerance};
                request = reflectedTimeSeries;
            }
            else
            {
                basicTimeSeries = new PostBasicTimeSeries {GapTolerance = gapTolerance};
                request = basicTimeSeries;
            }

            request.LocationUniqueId = location.UniqueId;
            request.UtcOffset = Context.UtcOffset ?? location.UtcOffset;
            request.Label = timeSeriesInfo.Label;
            request.Parameter = parameter.ParameterId;
            request.Description = Context.Description;
            request.Comment = Context.Comment;
            request.Unit = Context.Unit ?? parameter.UnitIdentifier;
            request.InterpolationType = interpolationType;
            request.Publish = Context.Publish;
            request.Method = Context.Method ?? defaultMonitoringMethod.MethodCode;
            request.ComputationIdentifier = Context.ComputationIdentifier;
            request.ComputationPeriodIdentifier = Context.ComputationPeriodIdentifier;
            request.SubLocationIdentifier = Context.SubLocationIdentifier;
            request.ExtendedAttributeValues = MergeExtendedAttributesWithMandatoryExtendedAttributes(timeSeriesExtendedAttributes).ToList();

            Log.Info($"Creating '{timeSeriesIdentifier}' time-series ...");

            var timeSeries = Context.CreateMode == CreateMode.Reflected
                ? Client.Provisioning.Post(reflectedTimeSeries)
                : Client.Provisioning.Post(basicTimeSeries);

            Log.Info($"Created '{timeSeries.Identifier}' ({timeSeries.TimeSeriesType}) successfully.");
        }

        private static readonly InterpolationType[] InterpolationTypesWithNoGaps =
        {
            InterpolationType.InstantaneousTotals,
            InterpolationType.DiscreteValues,
        };

        private IEnumerable<ExtendedAttributeValue> MergeExtendedAttributesWithMandatoryExtendedAttributes(IList<ExtendedAttributeField> extendedAttributeFields)
        {
            var unknownAttributes = Context
                .ExtendedAttributeValues
                .Where(eav => extendedAttributeFields.All(f => eav.ColumnIdentifier != f.ColumnIdentifier))
                .ToList();

            if (unknownAttributes.Any())
            {
                Log.Warn($"Ignoring {unknownAttributes.Count} unknown extended attributes: {string.Join(", ", unknownAttributes.Select(a => $"{a.ColumnIdentifier}={a.Value}"))}");
            }

            return Context
                .ExtendedAttributeValues
                .Where(eav => extendedAttributeFields.Any(f => eav.ColumnIdentifier == f.ColumnIdentifier))
                .Concat(extendedAttributeFields
                    .Where(f => Context.ExtendedAttributeValues.All(eav => eav.ColumnIdentifier != f.ColumnIdentifier) && !f.CanBeEmpty)
                    .Select(CreateDefaultValue));
        }

        private static ExtendedAttributeValue CreateDefaultValue(ExtendedAttributeField field)
        {
            return new ExtendedAttributeValue
            {
                ColumnIdentifier = field.ColumnIdentifier,
                Value = DefaultValues[field.FieldType](field)
            };
        }

        private static readonly Dictionary<ExtendedAttributeFieldType, Func<ExtendedAttributeField, string>>
            DefaultValues = new Dictionary<ExtendedAttributeFieldType, Func<ExtendedAttributeField, string>>
            {
                {ExtendedAttributeFieldType.Boolean, field => default(bool).ToString()},
                {ExtendedAttributeFieldType.Number, field => "0"},
                {ExtendedAttributeFieldType.DateTime, field => DateTimeOffset.UtcNow.ToString("O")},
                {ExtendedAttributeFieldType.String, field => string.Empty},
                {ExtendedAttributeFieldType.StringOption, field => field.ValueOptions.First()},
            };
    }
}
