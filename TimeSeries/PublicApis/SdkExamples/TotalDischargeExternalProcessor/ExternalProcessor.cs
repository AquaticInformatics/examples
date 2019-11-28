using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;
using PostReflectedTimeSeries = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.PostReflectedTimeSeries;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;

namespace TotalDischargeExternalProcessor
{
    public class ExternalProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private IAquariusClient Client { get; set; }

        public void Run()
        {
            Validate();

            using (Client = CreateConnectedClient())
            {
                LoadConfiguration();

                foreach (var processor in Processors)
                {
                    Calculate(processor);
                }
            }
        }

        private void Validate()
        {
            ThrowIfEmpty(nameof(Context.Server), Context.Server);
            ThrowIfEmpty(nameof(Context.Username), Context.Username);
            ThrowIfEmpty(nameof(Context.Password), Context.Password);

            if (!Context.Processors.Any())
                throw new ExpectedException($"No processors configured. Nothing to do. Add a /{nameof(Context.Processors)}= option or positional argument.");
        }

        private void ThrowIfEmpty(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            throw new ExpectedException($"/{name}= value is required.");
        }

        private IAquariusClient CreateConnectedClient()
        {
            Log.Info($"Connecting to {Context.Server} ...");

            var client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password);

            Log.Info($"Connected to {Context.Server} ({client.ServerVersion}) as '{Context.Username}'");

            return client;
        }

        private List<Parameter> Parameters { get; set; }
        private List<PopulatedUnitGroup> UnitGroups { get; set; }
        private List<TimeSeriesDescription> TimeSeriesDescriptions { get; set; }
        private List<Processor> Processors { get; set; }

        private void LoadConfiguration()
        {
            Log.Info($"Loading configuration ...");
            Parameters = Client.Provisioning.Get(new GetParameters()).Results;
            UnitGroups = Client.Provisioning.Get(new GetUnits()).Results;

            TimeSeriesDescriptions = Client.Publish.Get(new TimeSeriesDescriptionServiceRequest()).TimeSeriesDescriptions;

            Log.Info($"{Parameters.Count} parameters, {UnitGroups.Count} unit groups, and {TimeSeriesDescriptions.Count} time-series.");

            Processors = Context
                .Processors
                .Select(Resolve)
                .OrderBy(p => p.DischargeTotalTimeSeries.LocationIdentifier)
                .ThenBy(p => p.DischargeTotalTimeSeries.Identifier)
                .ToList();

            Log.Info($"Resolved {Processors.Count} external processor configurations.");
        }

        private Processor Resolve(ProcessorConfig processorConfig)
        {
            var processor = new Processor
            {
                EventTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processorConfig.EventTimeSeries), processorConfig.EventTimeSeries)),
                DischargeTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processorConfig.DischargeTimeSeries), processorConfig.DischargeTimeSeries)),
                DischargeTotalTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processorConfig.DischargeTotalTimeSeries), processorConfig.DischargeTotalTimeSeries)),
                MinimumEventDuration = processorConfig.MinimumEventDuration ?? Context.MinimumEventDuration
            };

            ThrowIfWrongParameter("QR", nameof(processor.DischargeTimeSeries), processor.DischargeTimeSeries);
            ThrowIfWrongParameter("QV", nameof(processor.DischargeTotalTimeSeries), processor.DischargeTotalTimeSeries);
            ThrowIfWrongTimeSeriesType(TimeSeriesType.Reflected, nameof(processor.DischargeTotalTimeSeries), processor.DischargeTotalTimeSeries);

            DecomposeVolumetricFlowUnits(processor.DischargeTimeSeries);

            return processor;
        }

        private TimeSeries GetTimeSeries(TimeSeriesDescription timeSeries)
        {
            return Client.Provisioning.Get(new GetTimeSeries {TimeSeriesUniqueId = timeSeries.UniqueId});
        }

        private TimeSeriesDescription FindTimeSeries(string name, string identifierOrUniqueId)
        {
            if (Guid.TryParse(identifierOrUniqueId, out var uniqueId))
            {
                return FindTimeSeries(name, uniqueId);
            }

            var timeSeries = TimeSeriesDescriptions
                .FirstOrDefault(ts =>
                    ts.Identifier.Equals(identifierOrUniqueId, StringComparison.InvariantCultureIgnoreCase));

            if (timeSeries != null)
                return timeSeries;

            throw new ExpectedException($"'{identifierOrUniqueId}' is not a known {name} time-series.");
        }

        private TimeSeriesDescription FindTimeSeries(string name, Guid uniqueId)
        {
            var timeSeries = TimeSeriesDescriptions
                .FirstOrDefault(ts => ts.UniqueId == uniqueId);

            if (timeSeries != null)
                return timeSeries;

            throw new ExpectedException($"{uniqueId:N} is not a known {name} time-series.");
        }

        private void ThrowIfWrongParameter(string parameterId, string name, TimeSeries timeSeries)
        {
            var parameter = Parameters
                .FirstOrDefault(p => p.ParameterId.Equals(timeSeries.Parameter));

            if (parameter == null)
                throw new ExpectedException($"Unknown '{timeSeries.Parameter}' parameter");

            if (!parameter.ParameterId.Equals(parameterId))
                throw new ExpectedException($"{name} '{timeSeries.Identifier}' is not the expected '{parameterId}' parameter.");
        }

        private void ThrowIfWrongTimeSeriesType(TimeSeriesType timeSeriesType, string name, TimeSeries timeSeries)
        {
            if (timeSeries.TimeSeriesType == timeSeriesType)
                return;

            throw new ExpectedException($"{name} '{timeSeries.Identifier}' ({timeSeries.TimeSeriesType}) is not the expected '{timeSeriesType}' time-series type.");
        }

        private (Unit VolumeUnit, Unit TimeUnit) DecomposeVolumetricFlowUnits(TimeSeries timeSeries)
        {
            var parts = timeSeries.Unit.Split(TimeSeparatorChars, 2);

            if (parts.Length != 2)
                throw new ExpectedException($"The '{timeSeries.Unit}' unit of '{timeSeries.Identifier}' is not a supported volumetric flow unit");

            var volumeUnitId = parts[0];
            var timeUnitId = parts[1];

            var volumeUnit = GetVolumeUnit(volumeUnitId);
            var timeUnit = GetTimeUnit(timeUnitId);

            return (volumeUnit, timeUnit);
        }

        private static readonly char[] TimeSeparatorChars = {'/'};

        private Unit GetTimeUnit(string unitId)
        {
            return GetUnit("Time", unitId);
        }

        private Unit GetVolumeUnit(string unitId)
        {
            return GetUnit("Volume", unitId);
        }

        private Unit GetUnit(string unitGroupId, string unitId)
        {
            var unitGroup = UnitGroups
                .FirstOrDefault(ug => ug.GroupIdentifier == unitGroupId);

            if (unitGroup == null)
                throw new ExpectedException($"'{unitGroupId}' is not a known unit group.");

            var unit = unitGroup
                .Units
                .FirstOrDefault(u => u.UnitIdentifier == unitId);

            if (unit == null)
                throw new ExpectedException($"'{unitId}' is not part of the '{unitGroup.GroupIdentifier}' unit group.");

            return unit;
        }

        private void Calculate(Processor processor)
        {
            Log.Info($"Re-calculating '{processor.DischargeTotalTimeSeries.Identifier}' from '{processor.DischargeTimeSeries.Identifier}' gated by '{processor.EventTimeSeries.Identifier}' ...");

            var eventPoints = LoadPoints(
                nameof(processor.EventTimeSeries),
                processor.EventTimeSeries);

            if (!eventPoints.Any())
            {
                return;
            }

            var queryFrom = GetZonedTime(processor.EventTimeSeries, eventPoints.First().Time);
            var queryTo = GetZonedTime(processor.EventTimeSeries, eventPoints.Last().Time);

            var dischargePoints = LoadPoints(
                nameof(processor.DischargeTimeSeries),
                processor.DischargeTimeSeries,
                queryFrom,
                queryTo);

            if (!dischargePoints.Any())
            {
                return;
            }

            var eventIntervals = new EventIntervalDetector(eventPoints, processor.MinimumEventDuration)
                .Detect()
                .ToList();

            Log.Info($"Detected {eventIntervals.Count} intervals in {nameof(processor.EventTimeSeries)} '{processor.EventTimeSeries.Identifier}'");

            var pointsToAppend = new List<TimeSeriesPoint>();

            foreach (var interval in eventIntervals)
            {
                var start = GetZonedTime(processor.EventTimeSeries, interval.Start);
                var end = GetZonedTime(processor.EventTimeSeries, interval.End);

                var eventDischarge = CalculateEventTotalDischarge(
                    processor.DischargeTotalTimeSeries,
                    interval,
                    processor.DischargeTimeSeries,
                    dischargePoints);

                if (pointsToAppend.Any())
                {
                    // Insert an explicit gap
                    pointsToAppend.Add(new TimeSeriesPoint
                    {
                        Type = PointType.Gap
                    });
                }

                pointsToAppend.Add(new TimeSeriesPoint
                {
                    Time = interval.Start,
                    Value = eventDischarge
                });

                pointsToAppend.Add(new TimeSeriesPoint
                {
                    Time = interval.End,
                    Value = eventDischarge
                });

                Log.Info($"Event Discharge = {eventDischarge:F3} ({processor.DischargeTotalTimeSeries.Unit}) {start:O} - {end:O} ({interval.Duration.ToTimeSpan().Humanize()})");
            }

            Log.Info($"Appending {pointsToAppend.Count} points to {nameof(processor.DischargeTotalTimeSeries)} '{processor.DischargeTotalTimeSeries.Identifier}' ...");

            var stopwatch = Stopwatch.StartNew();

            var result = Client.Acquisition.RequestAndPollUntilComplete(
                client => client.Post(new PostReflectedTimeSeries
                {
                    UniqueId = processor.DischargeTotalTimeSeries.UniqueId,
                    Points = pointsToAppend,
                    TimeRange =
                        new Interval( // Apply a 1-day margin, to workaround the AQ-23146 OverflowException crash
                            Instant.FromDateTimeOffset(DateTimeOffset.MinValue).Plus(Duration.FromStandardDays(1)),
                            Instant.FromDateTimeOffset(DateTimeOffset.MaxValue).Minus(Duration.FromStandardDays(1)))
                }),
                (client, response) => client.Get(new GetTimeSeriesAppendStatus
                {
                    AppendRequestIdentifier = response.AppendRequestIdentifier
                }),
                polledStatus => polledStatus.AppendStatus != AppendStatusCode.Pending);

            if (result.AppendStatus != AppendStatusCode.Completed)
                throw new ExpectedException($"Unexpected append status={result.AppendStatus}");

            Log.Info($"Appended {result.NumberOfPointsAppended} points (deleting {result.NumberOfPointsDeleted} points) in {stopwatch.Elapsed.Humanize()}");
        }

        private static DateTimeOffset GetZonedTime(TimeSeries timeSeries, Instant? instant)
        {
            if (!instant.HasValue)
                throw new ArgumentNullException(nameof(instant));

            return instant.Value.InZone(DateTimeZone.ForOffset(timeSeries.UtcOffset)).ToDateTimeOffset();
        }

        private List<Point> LoadPoints(string name, TimeSeries timeSeries, DateTimeOffset? queryFrom = null, DateTimeOffset? queryTo = null)
        {
            var message = string.Join(" ", new[]
                {
                    name,
                    $"'{timeSeries.Identifier}'",
                    queryFrom.HasValue ? $" from {queryFrom:O}" : null,
                    queryTo.HasValue ? $" to {queryTo:O}" : null,
                }
                .Where(s => !string.IsNullOrEmpty(s)));

            Log.Info($"Loading {message} ...");

            var points = Client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
                {
                    TimeSeriesUniqueId = timeSeries.UniqueId,
                    GetParts = "PointsOnly",
                    QueryFrom = queryFrom,
                    QueryTo = queryTo,
                    ReturnFullCoverage = queryFrom.HasValue || queryTo.HasValue,
                    IncludeGapMarkers = false
                }).Points
                .Where(p => p.Value.Numeric.HasValue)
                .Select(p => new Point
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset),
                    Value = p.Value.Numeric.Value
                })
                .ToList();

            if (!points.Any())
            {
                Log.Warn($"No {name} points found in '{timeSeries.Identifier}'. Skipping calculation.");
            }
            else
            {
                var start = GetZonedTime(timeSeries, points.First().Time);
                var end = GetZonedTime(timeSeries, points.Last().Time);

                Log.Info($"Loaded {points.Count} points from {name} '{timeSeries.Identifier}' Start={start:O} End={end:O}");
            }

            return points;
        }

        private double CalculateEventTotalDischarge(
            TimeSeries dischargeTotalSeries,
            Interval eventInterval,
            TimeSeries dischargeSeries,
            List<Point> dischargePoints)
        {
            var totalDischarge = 0.0;

            Point prevPoint = null;

            var secondsUnit = GetTimeUnit("s");
            var outputVolumeUnit = GetVolumeUnit(dischargeTotalSeries.Unit);

            var (volumeUnit, timeUnit) = DecomposeVolumetricFlowUnits(dischargeSeries);

            foreach (var point in dischargePoints)
            {
                if (point.Time < eventInterval.Start)
                {
                    prevPoint = point;
                    continue;
                }

                if (prevPoint != null)
                {
                    var interpolationTime = prevPoint.Time.Plus((point.Time - prevPoint.Time) / 2);
                    var duration = point.Time - prevPoint.Time;
                    var durationSeconds = duration.ToTimeSpan().TotalSeconds;

                    var interpolatedFlow = (prevPoint.Value + point.Value) / 2;
                    var timeUnitsPerSecond = ConvertUnits(1, timeUnit, secondsUnit);
                    var pointVolume = interpolatedFlow * durationSeconds / timeUnitsPerSecond;

                    var contribution = ConvertUnits(pointVolume, volumeUnit, outputVolumeUnit);

                    if (interpolationTime < eventInterval.Start)
                    {
                        // Only take the portion within the event
                        var portion = (point.Time - eventInterval.Start).ToTimeSpan().TotalSeconds / durationSeconds;
                        contribution *= portion;
                    }
                    else if (interpolationTime > eventInterval.End)
                    {
                        // Only take the porting within the event
                        var portion = (eventInterval.End - prevPoint.Time).ToTimeSpan().TotalSeconds / durationSeconds;
                        contribution *= portion;
                    }

                    totalDischarge += contribution;
                }

                if (point.Time >= eventInterval.End)
                    break;

                prevPoint = point;
            }

            return totalDischarge;
        }

        private double ConvertUnits(double sourceValue, Unit sourceUnit, Unit targetUnit)
        {
            if (double.IsNaN(sourceValue) || sourceUnit.UnitIdentifier == targetUnit.UnitIdentifier)
                return sourceValue;

            var valueInBaseUnit = (sourceValue + sourceUnit.BaseOffset) * sourceUnit.BaseMultiplier;

            return valueInBaseUnit / targetUnit.BaseMultiplier - targetUnit.BaseOffset;
        }
    }
}
