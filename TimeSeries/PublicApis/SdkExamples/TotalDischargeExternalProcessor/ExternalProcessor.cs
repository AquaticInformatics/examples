using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;

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

        private Processor Resolve(ProcessorConfig processor)
        {
            var processorConfiguration = new Processor
            {
                EventTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processor.EventTimeSeries), processor.EventTimeSeries)),
                DischargeTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processor.DischargeTimeSeries), processor.DischargeTimeSeries)),
                DischargeTotalTimeSeries = GetTimeSeries(FindTimeSeries(nameof(processor.DischargeTotalTimeSeries), processor.DischargeTotalTimeSeries)),
                MinimumEventDuration = processor.MinimumEventDuration ?? Context.MinimumEventDuration
            };

            ThrowIfWrongParameter("QR", nameof(processorConfiguration.DischargeTimeSeries), processorConfiguration.DischargeTimeSeries);
            ThrowIfWrongParameter("QV", nameof(processorConfiguration.DischargeTotalTimeSeries), processorConfiguration.DischargeTotalTimeSeries);
            ThrowIfWrongTimeSeriesType(TimeSeriesType.Reflected, nameof(processorConfiguration.DischargeTotalTimeSeries), processorConfiguration.DischargeTotalTimeSeries);

            return processorConfiguration;
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

            foreach (var interval in eventIntervals)
            {
                var start = GetZonedTime(processor.EventTimeSeries, interval.Start);
                var end = GetZonedTime(processor.EventTimeSeries, interval.End);

                var eventDischarge = CalculateEventDischarge(interval, dischargePoints);

                Log.Info($"Event Discharge = {eventDischarge:F3} {start:O} - {end:O} ({interval.Duration.ToTimeSpan().Humanize()})");
            }
        }

        private static DateTimeOffset GetZonedTime(TimeSeries timeSeries, Instant? instant)
        {
            if (!instant.HasValue)
                throw new ArgumentNullException(nameof(instant));

            return instant.Value.InZone(DateTimeZone.ForOffset(timeSeries.UtcOffset)).ToDateTimeOffset();
        }

        private List<TimeSeriesPoint> LoadPoints(string name, TimeSeries timeSeries, DateTimeOffset? queryFrom = null, DateTimeOffset? queryTo = null)
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
                .Select(p => new TimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset),
                    Value = p.Value.Numeric
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

        private double CalculateEventDischarge(Interval interval, List<TimeSeriesPoint> points)
        {
            return 0;
        }
    }
}
