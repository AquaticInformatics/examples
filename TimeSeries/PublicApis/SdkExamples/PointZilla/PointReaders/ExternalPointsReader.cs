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
using ServiceStack;
using ServiceStack.Logging;
using Get3xCorrectedData = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDataCorrectedServiceRequest;
using Get3xTimeSeriesDescription = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDescriptionServiceRequest;
using Get3xCorrectionList = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.CorrectionListServiceRequest;
using Publish3xCorrection = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.Correction;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;
using TimeSeriesDataCorrectedServiceRequest = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesDataCorrectedServiceRequest;
using CorrectionListServiceRequest = Aquarius.TimeSeries.Client.ServiceModels.Publish.CorrectionListServiceRequest;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;
using TimeSeriesNote = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesNote;
using PublishGrade = Aquarius.TimeSeries.Client.ServiceModels.Publish.Grade;
using PublishQualifier = Aquarius.TimeSeries.Client.ServiceModels.Publish.Qualifier;
using PublishNote = Aquarius.TimeSeries.Client.ServiceModels.Publish.Note;
using PublishCorrection = Aquarius.TimeSeries.Client.ServiceModels.Publish.Correction;

namespace PointZilla.PointReaders
{
    public class ExternalPointsReader : PointReaderBase, IPointReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<TimeSeriesNote> Notes { get; } = new List<TimeSeriesNote>();

        public ExternalPointsReader(Context context)
            : base(context)
        {
        }

        public (List<TimeSeriesPoint> Points, List<TimeSeriesNote> Notes) LoadPoints()
        {
            var server = !string.IsNullOrEmpty(Context.SourceTimeSeries.Server) ? Context.SourceTimeSeries.Server : Context.Server;
            var username = !string.IsNullOrEmpty(Context.SourceTimeSeries.Username) ? Context.SourceTimeSeries.Username : Context.Username;
            var password = !string.IsNullOrEmpty(Context.SourceTimeSeries.Password) ? Context.SourceTimeSeries.Password : Context.Password;

            Log.Info($"Connecting to {server} to retrieve points ...");

            using (var client = CreateConnectedClient(server, username, password))
            {
                Log.Info($"Connected to {server} ({client.ServerVersion})");

                return (client.ServerVersion.IsLessThan(MinimumNgVersion)
                    ? LoadPointsFrom3X(client)
                    : LoadPointsFromNg(client), Notes);
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

            var gradesLookup = new MetadataLookup<PublishGrade>(timeSeriesData.Grades);
            var qualifiersLookup = new MetadataLookup<PublishQualifier>(timeSeriesData.Qualifiers);

            if (!Context.IgnoreNotes)
                Notes.AddRange(LoadAllNotes(client, timeSeriesInfo, timeSeriesData.Notes));

            var points = timeSeriesData
                .Points
                .Select(p => new TimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset),
                    Value = p.Value.Numeric,
                    GradeCode = gradesLookup.ResolveSingleMetadata(p.Timestamp.DateTimeOffset, g => int.Parse(g.GradeCode)),
                    Qualifiers = qualifiersLookup.ResolveOverlappingMetadata(p.Timestamp.DateTimeOffset, q => q.Identifier).ToList()
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

            Log.Info($"Loaded {PointSummarizer.Summarize(points)} and {"note".ToQuantity(Notes.Count)} from {timeSeriesInfo.Identifier}");

            return points;
        }

        private IEnumerable<TimeSeriesNote> LoadAllNotes(IAquariusClient client, TimeSeries timeSeriesInfo, List<PublishNote> notes)
        {
            var corrections = client.Publish.Get(new CorrectionListServiceRequest
            {
                TimeSeriesUniqueId = timeSeriesInfo.UniqueId,
                QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
            }).Corrections;

            var utcOffset = timeSeriesInfo.UtcOffset.ToTimeSpan();

            return notes
                .Select(ConvertNgNote)
                .Concat(corrections
                    .Select(c => ConvertNgCorrection(utcOffset, c)));
        }

        private static TimeSeriesNote ConvertNgNote(PublishNote note)
        {
            return new TimeSeriesNote
            {
                TimeRange = new Interval(Instant.FromDateTimeOffset(note.StartTime), Instant.FromDateTimeOffset(note.EndTime)),
                NoteText = note.NoteText
            };
        }

        private static TimeSeriesNote ConvertNgCorrection(TimeSpan utcOffset, PublishCorrection correction)
        {
            return new TimeSeriesNote
            {
                TimeRange = new Interval(Instant.FromDateTimeOffset(correction.StartTime), Instant.FromDateTimeOffset(correction.EndTime)),
                NoteText = string.Join("\r\n", new[]
                    {
                        correction.Comment,
                        $"{correction.Type} correction added by {correction.User} @ {FriendlyTimestamp(utcOffset, correction.AppliedTimeUtc)} with {correction.ProcessingOrder} processing order.",
                    }
                    .Concat(correction.Parameters?.Any() ?? false
                        ? correction.Parameters.SelectMany(kvp => FormatParameterValue($"{correction.Type}.Parameter", kvp.Key, kvp.Value))
                        : Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            };
        }

        private static TimeSeriesNote Convert3XCorrection(TimeSpan utcOffset, Publish3xCorrection correction)
        {
            return new TimeSeriesNote
            {
                TimeRange = new Interval(Instant.FromDateTimeOffset(correction.StartTime), Instant.FromDateTimeOffset(correction.EndTime)),
                NoteText = string.Join("\r\n", new[]
                    {
                        correction.Comment,
                        $"{correction.Type} correction added by {correction.User} @ {FriendlyTimestamp(utcOffset, correction.AppliedTime.UtcDateTime)}",
                    }
                    .Concat(correction.Parameters?.Any() ?? false
                        ? correction.Parameters.SelectMany(kvp => FormatParameterValue($"{correction.Type}.Parameter", kvp.Key, kvp.Value))
                        : Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            };
        }

        private static string FriendlyTimestamp(TimeSpan utcOffset, DateTime dateTimeUtc)
        {
            if (dateTimeUtc.Kind != DateTimeKind.Utc)
            {
                dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc);
            }

            // Convert from UTC to time-series local
            var dateTime = DateTime.SpecifyKind(dateTimeUtc + utcOffset, DateTimeKind.Unspecified);

            if (dateTime.TimeOfDay == TimeSpan.Zero)
            {
                // No need to clutter the comment with 00:00:00.0000000
                return $"{dateTime:yyyy-MM-dd}";
            }

            // Full-ish precision. No need for seconds/subseconds for these human-driven edits
            return $"{dateTime:yyyy-MM-dd HH:mm}";
        }

        private static IEnumerable<string> FormatParameterValue(string label, string key, object obj)
        {
            switch (obj)
            {
                case IDictionary<string, object> value:
                    return value
                        .SelectMany(kvp => FormatParameterValue($"{label}.{key}", kvp.Key, kvp.Value));

                case string value:
                    if (value.StartsWith("{") && value.EndsWith("}"))
                    {
                        var dict = value.FromJson<Dictionary<string, object>>();

                        return dict
                            .SelectMany(kvp => FormatParameterValue($"{label}.{key}", kvp.Key, kvp.Value));
                    }
                    else if (value.StartsWith("[") && value.EndsWith("]"))
                    {
                        var values = value.FromJsv<List<Dictionary<string, object>>>();

                        return values
                            .SelectMany((v,i) => FormatParameterValue($"{label}.{key}", $"[{i}]", v));
                    }
                    else
                        return new[] { $"{label}.{key}: {value}" };

                case bool value:
                    return new[] { $"{label}.{key}: {value}" };

                case int value:
                    return new[] { $"{label}.{key}: {value}" };

                case double value:
                    return new[] { $"{label}.{key}: {value}" };
            }

            throw new ExpectedException($"Unsupported parameter type {obj.GetType().Name} for {label} {key}={obj}");
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

            if (!Context.IgnoreNotes)
            {
                var corrections = client.Publish.Get(new Get3xCorrectionList
                {
                    TimeSeriesIdentifier = Context.SourceTimeSeries.Identifier,
                    QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                    QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
                }).Corrections;

                var utcTimespan = utcOffset.ToTimeSpan();
                Notes.AddRange(corrections.Select(c => Convert3XCorrection(utcTimespan, c)));
            }

            Log.Info($"Loaded {PointSummarizer.Summarize(points)} and {"note".ToQuantity(Notes.Count)} from {Context.SourceTimeSeries.Identifier}");

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
