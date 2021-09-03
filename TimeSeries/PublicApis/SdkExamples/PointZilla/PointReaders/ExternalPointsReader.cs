using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;
using Get3xCorrectedData = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDataCorrectedServiceRequest;
using Get3xTimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDescriptionServiceRequest;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;
using TimeRange = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeRange;
using TimeSeriesDataCorrectedServiceRequest = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesDataCorrectedServiceRequest;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;

namespace PointZilla.PointReaders
{
    public class ExternalPointsReader : PointReaderBase, IPointReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ExternalPointsReader(Context context)
            : base(context)
        {
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

            var gradesLookup = new MetadataLookup<Aquarius.TimeSeries.Client.ServiceModels.Publish.Grade>(timeSeriesData.Grades);
            var qualifiersLookup = new MetadataLookup<Aquarius.TimeSeries.Client.ServiceModels.Publish.Qualifier>(timeSeriesData.Qualifiers);

            var points = timeSeriesData
                .Points
                .Select(p => new TimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset),
                    Value = p.Value.Numeric,
                    GradeCode = GetFirstMetadata(gradesLookup, p.Timestamp.DateTimeOffset, g => int.Parse(g.GradeCode)),
                    Qualifiers = GetManyMetadata(qualifiersLookup, p.Timestamp.DateTimeOffset, q => q.Identifier).ToList()
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

        public class MetadataLookup<TMetadata> where TMetadata : TimeRange
        {
            private IEnumerator<TMetadata> Enumerator { get; }
            private TMetadata CurrentItem { get; set; }
            private List<TMetadata> CandidateItems { get; } = new List<TMetadata>();

            public MetadataLookup(IEnumerable<TMetadata> items)
            {
                Enumerator = items.GetEnumerator();

                AdvanceEnumerator();
            }

            private void AdvanceEnumerator()
            {
                CurrentItem = Enumerator.MoveNext()
                    ? Enumerator.Current
                    : null;
            }

            public TMetadata FirstOrDefault(DateTimeOffset timestamp)
            {
                do
                {
                    if (IsItemValid(CurrentItem, timestamp))
                        return CurrentItem;

                    if (IsItemExpired(CurrentItem, timestamp))
                    {
                        AdvanceEnumerator();
                    }
                    else
                    {
                        return null;
                    }

                } while (true);
            }

            private static bool IsItemValid(TMetadata item, DateTimeOffset timestamp)
            {
                return item?.StartTime <= timestamp && timestamp < item.EndTime;
            }

            private static bool IsItemExpired(TMetadata item, DateTimeOffset timestamp)
            {
                return item?.EndTime <= timestamp;
            }

            public IEnumerable<TMetadata> GetMany(DateTimeOffset timestamp)
            {
                if (IsItemValid(CurrentItem, timestamp))
                {
                    while (IsItemValid(CurrentItem, timestamp))
                    {
                        CandidateItems.Add(CurrentItem);

                        AdvanceEnumerator();
                    }
                }

                var expiredItems = CandidateItems
                    .Where(item => IsItemExpired(item, timestamp))
                    .ToList();

                if (expiredItems.Any())
                {
                    CandidateItems.RemoveAll(item => IsItemExpired(item, timestamp));
                }

                return CandidateItems
                    .Where(item => IsItemValid(item, timestamp));
            }
        }

        private static T GetFirstMetadata<TMetadata, T>(MetadataLookup<TMetadata> lookup, DateTimeOffset time, Func<TMetadata,T> func)
            where TMetadata : TimeRange
        {
            var metadata = lookup.FirstOrDefault(time);

            return metadata == null ? default : func(metadata);
        }

        private static IEnumerable<T> GetManyMetadata<TMetadata, T>(MetadataLookup<TMetadata> lookup, DateTimeOffset time, Func<TMetadata, T> func)
            where TMetadata : TimeRange
        {
            return lookup
                .GetMany(time)
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
