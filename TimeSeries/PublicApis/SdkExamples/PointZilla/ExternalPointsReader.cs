using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Get3xCorrectedData = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDataCorrectedServiceRequest;
using Get3xTimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDescriptionServiceRequest;
using NodaTime;
using ServiceStack.Logging;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;

namespace PointZilla
{
    public class ExternalPointsReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public ExternalPointsReader(Context context)
        {
            Context = context;
        }

        public List<ReflectedTimeSeriesPoint> LoadPoints()
        {
            var server = !string.IsNullOrEmpty(Context.SourceTimeSeries.Server) ? Context.SourceTimeSeries.Server : Context.Server;
            var username = !string.IsNullOrEmpty(Context.SourceTimeSeries.Username) ? Context.SourceTimeSeries.Username : Context.Username;
            var password = !string.IsNullOrEmpty(Context.SourceTimeSeries.Password) ? Context.SourceTimeSeries.Password : Context.Password;

            using (var client = AquariusClient.CreateConnectedClient(server, username, password))
            {
                Log.Info($"Connected to {server} ({client.ServerVersion})");

                return client.ServerVersion.IsLessThan(MinimumNgVersion)
                    ? LoadPointsFrom3X(client)
                    : LoadPointsFromNg(client);
            }
        }

        private static readonly AquariusServerVersion MinimumNgVersion = AquariusServerVersion.Create("14");

        private List<ReflectedTimeSeriesPoint> LoadPointsFromNg(IAquariusClient client)
        {
            var timeSeriesInfo = client.GetTimeSeriesInfo(Context.SourceTimeSeries.Identifier);

            var timeSeriesData = client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesInfo.UniqueId,
                QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
            });

            var points = timeSeriesData
                .Points
                .Select(p => new ReflectedTimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset),
                    Value = p.Value.Numeric,
                    GradeCode = GetFirstMetadata(timeSeriesData.Grades, p.Timestamp.DateTimeOffset, g => int.Parse(g.GradeCode)),
                    Qualifiers = GetManyMetadata(timeSeriesData.Qualifiers, p.Timestamp.DateTimeOffset, q => q.Identifier).ToList()
                })
                .ToList();

            var gapToleranceInMinutes = timeSeriesData.GapTolerances.Last().ToleranceInMinutes;
            var gapTolerance = gapToleranceInMinutes.HasValue
                ? Duration.FromMinutes((long) gapToleranceInMinutes.Value)
                : DurationExtensions.MaxGapDuration;
            var interpolationType = (InterpolationType) Enum.Parse(typeof(InterpolationType), timeSeriesData.InterpolationTypes.Last().Type, true);

            SetTimeSeriesCreationProperties(
                timeSeriesInfo,
                timeSeriesInfo.UtcOffset,
                timeSeriesData.Methods.LastOrDefault()?.MethodCode,
                gapTolerance,
                interpolationType);

            Log.Info($"Loaded {points.Count} points from {timeSeriesInfo.Identifier}");

            return points;
        }

        private static T GetFirstMetadata<TMetadata, T>(IEnumerable<TMetadata> items, DateTimeOffset time, Func<TMetadata,T> func)
            where TMetadata : TimeRange
        {
            var metadata = items.FirstOrDefault(i => i.StartTime <= time && time < i.EndTime);

            return metadata == null ? default(T) : func(metadata);
        }

        private static IEnumerable<T> GetManyMetadata<TMetadata, T>(IEnumerable<TMetadata> items, DateTimeOffset time, Func<TMetadata, T> func)
            where TMetadata : TimeRange
        {
            return items
                .Where(i => i.StartTime <= time && time < i.EndTime)
                .Select(func);
        }

        private void SetTimeSeriesCreationProperties(
            TimeSeries timeSeries,
            Offset? utcOffset = null,
            string method = null,
            Duration? gapTolerance = null,
            InterpolationType? interpolationType = null)
        {
            if (gapTolerance.HasValue)
                Context.GapTolerance = gapTolerance.Value;

            if (interpolationType.HasValue && !Context.InterpolationType.HasValue)
                Context.InterpolationType = interpolationType;

            if (utcOffset.HasValue && !Context.UtcOffset.HasValue)
                Context.UtcOffset = utcOffset.Value;

            Context.Publish = timeSeries.Publish;
            Context.Description = timeSeries.Description;

            Context.Method = Context.Method ?? method;
            Context.Unit = Context.Unit ?? timeSeries.Unit;
            Context.Comment = Context.Comment ?? timeSeries.Comment;
            Context.ComputationIdentifier = Context.ComputationIdentifier ?? timeSeries.ComputationIdentifier;
            Context.ComputationPeriodIdentifier = Context.ComputationPeriodIdentifier ?? timeSeries.ComputationPeriodIdentifier;
            Context.SubLocationIdentifier = Context.SubLocationIdentifier ?? timeSeries.SubLocationIdentifier;

            foreach (var extendedAttributeValue in timeSeries.ExtendedAttributeValues)
            {
                if (Context.ExtendedAttributeValues.Any(a => a.ColumnIdentifier == extendedAttributeValue.ColumnIdentifier))
                    continue;

                Context.ExtendedAttributeValues.Add(extendedAttributeValue);
            }
        }

        private List<ReflectedTimeSeriesPoint> LoadPointsFrom3X(IAquariusClient client)
        {
            var timeSeriesDescription = client.Publish.Get(new Get3xTimeSeriesDescription
                {
                    LocationIdentifier = Context.SourceTimeSeries.LocationIdentifier,
                    Parameter = Context.SourceTimeSeries.Parameter
                })
                .TimeSeriesDescriptions
                .SingleOrDefault(ts => ts.Identifier == Context.SourceTimeSeries.Identifier);

            if (timeSeriesDescription == null)
                throw new ExpectedException($"Can't find '{Context.SourceTimeSeries.Identifier}' time-series in location '{Context.SourceTimeSeries.LocationIdentifier}'.");

            var points = client.Publish.Get(new Get3xCorrectedData
                {
                    TimeSeriesIdentifier = Context.SourceTimeSeries.Identifier,
                    QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                    QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
                })
                .Points
                .Select(p => new ReflectedTimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp),
                    Value = p.Value,
                    GradeCode = p.Grade
                })
                .ToList();

            SetTimeSeriesCreationProperties(new TimeSeries
            {
                Parameter = timeSeriesDescription.Parameter,
                Label = timeSeriesDescription.Label,
                Unit = timeSeriesDescription.Unit,
                Publish = timeSeriesDescription.Publish,
                Description = timeSeriesDescription.Description,
                Comment = timeSeriesDescription.Comment,
                ComputationIdentifier = timeSeriesDescription.ComputationIdentifier,
                ComputationPeriodIdentifier = timeSeriesDescription.ComputationPeriodIdentifier,
                SubLocationIdentifier = timeSeriesDescription.SubLocationIdentifier,
                LocationIdentifier = timeSeriesDescription.LocationIdentifier,
                ExtendedAttributeValues = timeSeriesDescription.ExtendedAttributes.Select(ea =>
                        new ExtendedAttributeValue
                        {
                            ColumnIdentifier = $"{ea.Name.ToUpperInvariant()}@TIMESERIES_EXTENSION",
                            Value = ea.Value.ToString()
                        })
                    .ToList()
            });

            Log.Info($"Loaded {points.Count} points from {Context.SourceTimeSeries.Identifier}");

            return points;
        }
    }
}
