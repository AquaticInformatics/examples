using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Humanizer;
using Get3xCorrectedData = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDataCorrectedServiceRequest;
using Get3xTimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDescriptionServiceRequest;
using NodaTime;
using ServiceStack.Logging;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;
using TimeRange = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeRange;
using TimeSeriesDataCorrectedServiceRequest = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesDataCorrectedServiceRequest;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;

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

        public List<TimeSeriesPoint> LoadPoints()
        {
            var server = !string.IsNullOrEmpty(Context.SourceTimeSeries.Server) ? Context.SourceTimeSeries.Server : Context.Server;
            var username = !string.IsNullOrEmpty(Context.SourceTimeSeries.Username) ? Context.SourceTimeSeries.Username : Context.Username;
            var password = !string.IsNullOrEmpty(Context.SourceTimeSeries.Password) ? Context.SourceTimeSeries.Password : Context.Password;

            Log.Info($"Connecting to {server} to retrieve points ...");

            using (var client = CreateConnectedClient(server, username, password))
            {
                Log.Info($"Connected to {server} ({client.ServerVersion})");

                return client.ServerVersion.IsLessThan(MinimumNgVersion)
                    ? LoadPointsFrom3X(client)
                    : LoadPointsFromNg(client);
            }
        }


        private IAquariusClient CreateConnectedClient(string server, string username, string password)
        {
            return string.IsNullOrWhiteSpace(Context.SessionToken) || server != Context.Server
                ? AquariusClient.CreateConnectedClient(server, username, password)
                : AquariusClient.ClientFromExistingSession(Context.Server, Context.SessionToken);
        }

        private static readonly AquariusServerVersion MinimumNgVersion = AquariusServerVersion.Create("14");

        private List<TimeSeriesPoint> LoadPointsFromNg(IAquariusClient client)
        {
            var timeSeriesInfo = client.GetTimeSeriesInfo(Context.SourceTimeSeries.Identifier);

            Log.Info($"Loading points from '{timeSeriesInfo.Identifier}' ...");

            var timeSeriesData = client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesInfo.UniqueId,
                QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
            });

            var points = timeSeriesData
                .Points
                .Select(p => new TimeSeriesPoint
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
                timeSeriesData.Methods.LastOrDefault()?.MethodCode,
                gapTolerance,
                interpolationType);

            Log.Info($"Loaded {"point".ToQuantity(points.Count)} from {timeSeriesInfo.Identifier}");

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
            string method = null,
            Duration? gapTolerance = null,
            InterpolationType? interpolationType = null)
        {
            if (gapTolerance.HasValue)
                Context.GapTolerance = gapTolerance.Value;

            if (interpolationType.HasValue && !Context.InterpolationType.HasValue)
                Context.InterpolationType = interpolationType;

            Context.Publish = timeSeries.Publish;
            Context.Description = timeSeries.Description;

            Context.UtcOffset = Context.UtcOffset ?? timeSeries.UtcOffset;
            Context.Method = Context.Method ?? method;
            Context.Unit = Context.Unit ?? timeSeries.Unit;
            Context.Comment = Context.Comment ?? timeSeries.Comment;
            Context.ComputationIdentifier = Context.ComputationIdentifier ?? timeSeries.ComputationIdentifier;
            Context.ComputationPeriodIdentifier = Context.ComputationPeriodIdentifier ?? timeSeries.ComputationPeriodIdentifier;
            Context.SubLocationIdentifier = Context.SubLocationIdentifier ?? timeSeries.SubLocationIdentifier;
            Context.TimeSeriesType = Context.TimeSeriesType ?? timeSeries.TimeSeriesType;

            foreach (var extendedAttributeValue in timeSeries.ExtendedAttributeValues)
            {
                if (Context.ExtendedAttributeValues.Any(a => a.ColumnIdentifier == extendedAttributeValue.ColumnIdentifier))
                    continue;

                Context.ExtendedAttributeValues.Add(extendedAttributeValue);
            }
        }


        private List<TimeSeriesPoint> LoadPointsFrom3X(IAquariusClient client)
        {
            var timeSeriesDescription = client.Publish.Get(new Get3xTimeSeriesDescription
                {
                    LocationIdentifier = Context.SourceTimeSeries.LocationIdentifier,
                    Parameter = Context.SourceTimeSeries.Parameter
                })
                .TimeSeriesDescriptions
                .SingleOrDefault(ts => ts.Identifier == Context.SourceTimeSeries.TargetIdentifier);

            if (timeSeriesDescription == null)
                throw new ExpectedException($"Can't find '{Context.SourceTimeSeries.Identifier}' time-series in location '{Context.SourceTimeSeries.LocationIdentifier}'.");

            Log.Info($"Loading points from '{timeSeriesDescription.Identifier}' ...");

            var correctedData = client.Publish.Get(new Get3xCorrectedData
            {
                TimeSeriesIdentifier = Context.SourceTimeSeries.Identifier,
                QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
            });

            var points = correctedData
                .Points
                .Select(p => new TimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp),
                    Value = p.Value,
                    GradeCode = p.Grade
                })
                .ToList();

            // 3.X Publish API's TimeSeriesDescription is missing some info, so grab those pieces from elsewhere

            // The time-range start will always be in the offset of the time-series, even when no points exist
            var utcOffset = Offset.FromHoursAndMinutes(correctedData.TimeRange.StartTime.Offset.Hours, correctedData.TimeRange.StartTime.Offset.Minutes);

            // We can infer the interpolationType from the last point (if one exists)
            var interpolationType = Context.InterpolationType ?? (correctedData.Points.Any()
                                        ? (InterpolationType?)correctedData.Points.Last().Interpolation
                                        : null);

            var timeSeries = new TimeSeries
            {
                Identifier = Context.SourceTimeSeries.Identifier,
                Parameter = timeSeriesDescription.Parameter,
                Label = timeSeriesDescription.Label,
                Unit = timeSeriesDescription.Unit,
                Publish = timeSeriesDescription.Publish,
                Description = timeSeriesDescription.Description,
                Comment = timeSeriesDescription.Comment,
                TimeSeriesType = KnownTimeSeriesTypes[timeSeriesDescription.TimeSeriesType],
                UtcOffset = utcOffset,
                ComputationIdentifier = timeSeriesDescription.ComputationIdentifier,
                ComputationPeriodIdentifier = timeSeriesDescription.ComputationPeriodIdentifier,
                SubLocationIdentifier = timeSeriesDescription.SubLocationIdentifier,
                LocationIdentifier = timeSeriesDescription.LocationIdentifier,
                ExtendedAttributeValues = timeSeriesDescription.ExtendedAttributes.Select(ea =>
                        new ExtendedAttributeValue
                        {
                            ColumnIdentifier = $"{ea.Name.ToUpperInvariant()}@TIMESERIES_EXTENSION",
                            Value = ea.Value?.ToString()
                        })
                    .ToList()
            };

            SetTimeSeriesCreationProperties(timeSeries, interpolationType: interpolationType);

            Log.Info($"Loaded {"point".ToQuantity(points.Count)} from {Context.SourceTimeSeries.Identifier}");

            return points;
        }

        private static readonly Dictionary<AtomType, TimeSeriesType> KnownTimeSeriesTypes =
            new Dictionary<AtomType, TimeSeriesType>
            {
                { AtomType.TimeSeries_Basic, TimeSeriesType.ProcessorBasic},
                { AtomType.TimeSeries_Field_Visit, TimeSeriesType.Reflected},
                { AtomType.TimeSeries_Composite, TimeSeriesType.ProcessorDerived},
                { AtomType.TimeSeries_Rating_Curve_Derived, TimeSeriesType.ProcessorDerived},
                { AtomType.TimeSeries_Calculated_Derived, TimeSeriesType.ProcessorDerived},
                { AtomType.TimeSeries_External, TimeSeriesType.Reflected},
                { AtomType.TimeSeries_Statistical_Derived, TimeSeriesType.ProcessorDerived},
                { AtomType.TimeSeries_ProcessorBasic, TimeSeriesType.ProcessorBasic},
                { AtomType.TimeSeries_ProcessorDerived, TimeSeriesType.ProcessorDerived},
            };
    }
}
