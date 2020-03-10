using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;
using InterpolationType = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.InterpolationType;
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

            if (string.IsNullOrEmpty(Context.ConfigPath))
            {
                Context.ConfigPath = Path.Combine(ExeHelper.ExeDirectory, $"{nameof(Config)}.json");
            }

            ThrowIfFileMissing(nameof(Context.ConfigPath), Context.ConfigPath);
        }

        private void ThrowIfEmpty(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            throw new ExpectedException($"/{name}= value is required.");
        }

        private void ThrowIfFileMissing(string name, string path)
        {
            if (File.Exists(path))
                return;

            throw new ExpectedException($"'{path}' not found. Set the /{name}= option to a valid file.");
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
        private List<LocationDescription> LocationDescriptions { get; set; }
        private List<Processor> Processors { get; set; }

        private void LoadConfiguration()
        {
            Log.Info($"Loading configuration ...");
            Parameters = Client.Provisioning.Get(new GetParameters()).Results;
            UnitGroups = Client.Provisioning.Get(new GetUnits()).Results;

            TimeSeriesDescriptions = Client.Publish.Get(new TimeSeriesDescriptionServiceRequest()).TimeSeriesDescriptions;
            LocationDescriptions = Client.Publish.Get(new LocationDescriptionListServiceRequest()).LocationDescriptions;

            Log.Info($"{Parameters.Count} parameters, {UnitGroups.Count} unit groups, {TimeSeriesDescriptions.Count} time-series, and {LocationDescriptions.Count} locations.");

            var config = new ConfigLoader()
                .Load(Context.ConfigPath);

            Processors = config
                .Processors
                .Select(p => Resolve(config.Defaults, p))
                .OrderBy(p => p.DischargeTotalTimeSeries.LocationIdentifier)
                .ToList();

            Log.Info($"Resolved {Processors.Count} external processor configurations.");
        }

        private const string DischargeParameterId = "QR";
        private const string DischargeTotalParameterId = "QV";

        private Processor Resolve(Defaults defaults, ProcessorConfig processorConfig)
        {
            var eventIdentifier = !string.IsNullOrEmpty(processorConfig.EventTimeSeries)
                ? processorConfig.EventTimeSeries
                : defaults.EventParameterAndLabel;

            var eventTimeSeries = FindTimeSeries(
                nameof(processorConfig.EventTimeSeries),
                eventIdentifier,
                processorConfig.Location,
                defaults.EventLabel);

            var dischargeIdentifier =
                ResolveDefaultIdentifier(processorConfig.DischargeTimeSeries, DischargeParameterId, defaults.DischargeLabel);

            var dischargeTimeSeries = FindTimeSeries(
                nameof(processorConfig.DischargeTimeSeries),
                dischargeIdentifier,
                processorConfig.Location,
                defaults.DischargeLabel);

            var dischargeTotalIdentifier =
                ResolveDefaultIdentifier(processorConfig.DischargeTotalTimeSeries, DischargeTotalParameterId, defaults.EventLabel);

            var dischargeTotalTimeSeries = FindOrCreateTimeSeries(
                nameof(processorConfig.DischargeTotalTimeSeries),
                dischargeTotalIdentifier,
                processorConfig.Location,
                defaults.EventLabel,
                identifier => CreateDischargeTotalTimeSeries(defaults, processorConfig, identifier));

            var processor = new Processor
            {
                EventTimeSeries = eventTimeSeries,
                DischargeTimeSeries = dischargeTimeSeries,
                DischargeTotalTimeSeries = dischargeTotalTimeSeries,
                MinimumEventDuration = processorConfig.MinimumEventDuration ?? defaults.MinimumEventDuration,
            };

            ThrowIfWrongParameter(DischargeParameterId, nameof(processor.DischargeTimeSeries), processor.DischargeTimeSeries);
            ThrowIfWrongParameter(DischargeTotalParameterId, nameof(processor.DischargeTotalTimeSeries), processor.DischargeTotalTimeSeries);
            ThrowIfWrongTimeSeriesType(TimeSeriesType.Reflected, nameof(processor.DischargeTotalTimeSeries), processor.DischargeTotalTimeSeries);

            DecomposeTimePeriodUnits(VolumeUnitGroup, processor.DischargeTimeSeries);

            // TODO: If no calculations, try to infer them from all the "LabData" timeseries in the location with a mass/volume unit group and a Total equivalent?

            processor.Calculations.AddRange(processorConfig
                .Calculations
                .Select(calculationConfig => Resolve(defaults, processorConfig.Location, processor, calculationConfig)));

            return processor;
        }

        private string ResolveDefaultIdentifier(string explicitIdentifier, string parameterId, string label)
        {
            if (!string.IsNullOrEmpty(explicitIdentifier))
                return explicitIdentifier;

            var parameter = FindExistingParameterById(parameterId);

            return $"{parameter.Identifier}.{label}";
        }

        private Calculation Resolve(Defaults defaults, string defaultLocation, Processor processor, CalculationConfig calculationConfig)
        {
            var samplingTimeSeries = FindTimeSeries(
                nameof(calculationConfig.SamplingSeries),
                calculationConfig.SamplingSeries,
                defaultLocation,
                defaults.SamplingLabel);

            var samplingIdentifier = TimeSeriesIdentifierParser.ParseIdentifier(samplingTimeSeries.Identifier);

            var totalLoadingIdentifier = !string.IsNullOrEmpty(calculationConfig.TotalLoadingSeries)
                ? calculationConfig.TotalLoadingSeries
                : $"{defaults.TotalLoadingPrefix}{samplingIdentifier.Parameter}";

            var totalLoadingTimeSeries = FindOrCreateTimeSeries(
                nameof(calculationConfig.TotalLoadingSeries),
                totalLoadingIdentifier,
                defaultLocation,
                defaults.EventLabel,
                identifier => CreateTotalLoadingTimeSeries(defaults, calculationConfig, processor, samplingTimeSeries, identifier));

            return new Calculation
            {
                SamplingTimeSeries = samplingTimeSeries,
                EventTimeSeries = processor.EventTimeSeries,
                TotalLoadingTimeSeries = totalLoadingTimeSeries
            };
        }

        private TimeSeries GetTimeSeries(Guid uniqueId)
        {
            return Client.Provisioning.Get(new GetTimeSeries {TimeSeriesUniqueId = uniqueId});
        }

        private const string DefaultMethodCode = "DefaultNone";

        private TimeSeries CreateDischargeTotalTimeSeries(
            Defaults defaults,
            ProcessorConfig processorConfig,
            string identifier)
        {
            var timeSeriesIdentifier = TimeSeriesIdentifierParser.ParseIdentifier(identifier);

            Log.Info($"Creating '{identifier}' ...");

            var parameter = FindExistingParameterByIdentifier(timeSeriesIdentifier.Parameter);
            var unit = ResolveParameterUnit(defaults.DischargeTotalUnit, processorConfig.DischargeTotalUnit, parameter);
            var location = FindExistingLocation(timeSeriesIdentifier.Location);

            var response = Client.Provisioning.Post(
                new Aquarius.TimeSeries.Client.ServiceModels.Provisioning.PostReflectedTimeSeries
                {
                    LocationUniqueId = location.UniqueId,
                    Parameter = parameter.ParameterId,
                    Unit = unit,
                    Label = timeSeriesIdentifier.Label,
                    InterpolationType = InterpolationType.SucceedingConstant,
                    GapTolerance = DurationExtensions.MaxGapDuration,
                    UtcOffset = location.UtcOffset,
                    Method = DefaultMethodCode,
                    Comment = $"Created automatically by {ExeHelper.ExeNameAndVersion}",
                    ExtendedAttributeValues = CreateRequiredExtendedAttributes()
                });

            Log.Info($"Created '{response.Identifier}' ({response.Unit})");

            AddNewTimeSeries(response);

            return response;
        }

        private string ResolveParameterUnit(string defaultUnit, string overrideUnit, Parameter parameter)
        {
            return !string.IsNullOrEmpty(overrideUnit)
                ? overrideUnit
                : !string.IsNullOrEmpty(defaultUnit)
                    ? defaultUnit
                    : parameter.UnitIdentifier;
        }

        private TimeSeries CreateTotalLoadingTimeSeries(
            Defaults defaults,
            CalculationConfig calculationConfig,
            Processor processor,
            TimeSeries sourceTimeSeries,
            string identifier)
        {
            var timeSeriesIdentifier = TimeSeriesIdentifierParser.ParseIdentifier(identifier);

            Log.Info($"Creating '{identifier}' ...");

            var parameter = FindExistingParameterByIdentifier(timeSeriesIdentifier.Parameter);
            var unit = ResolveParameterUnit(defaults.TotalLoadingUnit, calculationConfig.TotalLoadingUnit, parameter);
            var location = FindExistingLocation(timeSeriesIdentifier.Location);

            var (sourceMassUnit, sourceVolumeUnit) = DecomposeIntegrationUnits(MassUnitGroup, VolumeUnitGroup, sourceTimeSeries);
            var totalDischargeVolumeUnit = GetUnit(VolumeUnitGroup, processor.DischargeTotalTimeSeries.Unit);
            var loadingMassUnit = GetUnit(MassUnitGroup, parameter.UnitIdentifier);

            var comments = new List<string>();
            var scalars = new List<string>();

            if (sourceMassUnit != loadingMassUnit)
            {
                BuildUnitConversion(scalars, comments, sourceMassUnit, loadingMassUnit);
            }

            if (sourceVolumeUnit != totalDischargeVolumeUnit)
            {
                BuildUnitConversion(scalars, comments, totalDischargeVolumeUnit, sourceVolumeUnit);
            }

            var formula = !scalars.Any()
                ? $"y = x1 * x2; // No unit conversion required"
                : $"y = x1 * x2 * {string.Join(" * ", scalars)}; // Convert {string.Join(", then ", comments)}";

            var response = Client.Provisioning.Post(new PostCalculatedDerivedTimeSeries
            {
                LocationUniqueId = location.UniqueId,
                Parameter = parameter.ParameterId,
                Unit = unit,
                Label = timeSeriesIdentifier.Label,
                InterpolationType = GetTimeSeriesInterpolationType(sourceTimeSeries.UniqueId),
                UtcOffset = location.UtcOffset,
                Method = DefaultMethodCode,
                Comment = $"Created automatically by {ExeHelper.ExeNameAndVersion}",
                TimeSeriesUniqueIds = new List<Guid> { sourceTimeSeries.UniqueId, processor.DischargeTotalTimeSeries.UniqueId},
                Formula = formula,
                ExtendedAttributeValues = CreateRequiredExtendedAttributes()
            });

            Log.Info($"Created '{response.Identifier}' ({response.Unit}) with formula: {formula}");

            AddNewTimeSeries(response);

            return response;
        }

        private List<ExtendedAttributeValue> CreateRequiredExtendedAttributes()
        {
            var timeSeriesExtendedAttributes = Client.Provisioning.Get(new GetTimeSeriesExtendedAttributes())
                .Results;

            return timeSeriesExtendedAttributes
                .Where(f => !f.CanBeEmpty)
                .Select(CreateDefaultValue)
                .ToList();
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


        private void BuildUnitConversion(List<string> scalars, List<string> comments, Unit sourceUnit, Unit targetUnit)
        {
            var factor = ConvertUnits(1.0, sourceUnit, targetUnit);

            scalars.Add($"{factor}");
            comments.Add($"{sourceUnit.GroupIdentifier} ({sourceUnit.UnitIdentifier} to {targetUnit.UnitIdentifier})");
        }

        private Parameter FindExistingParameterByIdentifier(string identifier)
        {
            var parameter = Parameters
                .SingleOrDefault(p => p.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase));

            if (parameter != null)
                return parameter;

            parameter = Parameters
                .SingleOrDefault(p => p.ParameterId.Equals(identifier, StringComparison.InvariantCultureIgnoreCase));

            if (parameter != null)
                throw new ExpectedException($"'{identifier}' is not a known parameter identifier. Did you mean '{parameter.Identifier}' instead?");

            throw new ExpectedException($"'{identifier}' is not a known parameter identifier.");
        }

        private Parameter FindExistingParameterById(string parameterId)
        {
            var parameter = Parameters
                .SingleOrDefault(p => p.ParameterId.Equals(parameterId, StringComparison.InvariantCultureIgnoreCase));

            if (parameter != null)
                return parameter;
            throw new ExpectedException($"'{parameterId}' is not a known parameter ID.");
        }

        private Location FindExistingLocation(string locationIdentifier)
        {
            var location = LocationDescriptions
                .SingleOrDefault(l =>
                    l.Identifier.Equals(locationIdentifier, StringComparison.InvariantCultureIgnoreCase));

            if (location != null)
                return Client.Provisioning.Get(new GetLocation{ LocationUniqueId = location.UniqueId});

            throw new ExpectedException($"'{locationIdentifier}' is not a known location.");
        }

        private InterpolationType GetTimeSeriesInterpolationType(Guid uniqueId)
        {
            var response = Client.Publish.Get(new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = uniqueId,
                GetParts = "MetadataOnly",
            });
                
            return (InterpolationType)Enum.Parse(typeof(InterpolationType), $"{response.InterpolationTypes.Last().Type}", true);
        }

        private void AddNewTimeSeries(TimeSeries timeSeries)
        {
            TimeSeriesDescriptions.AddRange(Client.Publish.Get(new TimeSeriesDescriptionListByUniqueIdServiceRequest
            {
                TimeSeriesUniqueIds = new List<Guid> {timeSeries.UniqueId}
            }).TimeSeriesDescriptions);
        }

        private TimeSeries FindTimeSeries(
            string name,
            string identifierOrUniqueId,
            string defaultLocation,
            string defaultLabel)
        {
            return FindOrCreateTimeSeries(
                name,
                identifierOrUniqueId,
                defaultLocation,
                defaultLabel);
        }

        private TimeSeries FindOrCreateTimeSeries(
            string name,
            string identifierOrUniqueId,
            string defaultLocation,
            string defaultLabel,
            Func<string,TimeSeries> createFactory = null)
        {
            identifierOrUniqueId = identifierOrUniqueId ?? string.Empty;

            if (Guid.TryParse(identifierOrUniqueId, out var uniqueId))
            {
                return GetTimeSeries(FindExistingTimeSeries(name, uniqueId));
            }

            var inferredLabel = !identifierOrUniqueId.Contains(".") && !string.IsNullOrEmpty(defaultLabel)
                ? $".{defaultLabel}"
                : null;

            var inferredLocation = !identifierOrUniqueId.Contains("@") && !string.IsNullOrEmpty(defaultLocation)
                ? $"@{defaultLocation}"
                : null;

            var identifier = $"{identifierOrUniqueId}{inferredLabel}{inferredLocation}";

            var timeSeries = TimeSeriesDescriptions
                .FirstOrDefault(ts =>
                    ts.Identifier.Equals(identifier, StringComparison.InvariantCultureIgnoreCase));

            if (timeSeries != null)
                return GetTimeSeries(timeSeries.UniqueId);

            if (Context.CreateMissingTimeSeries && createFactory != null)
                return createFactory(identifier);

            throw new ExpectedException($"'{identifier}' is not a known {name} time-series.");
        }

        private Guid FindExistingTimeSeries(string name, Guid uniqueId)
        {
            var timeSeries = TimeSeriesDescriptions
                .FirstOrDefault(ts => ts.UniqueId == uniqueId);

            if (timeSeries != null)
                return timeSeries.UniqueId;

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

        private (Unit CoreUnit, Unit IntegrationUnit) DecomposeTimePeriodUnits(string coreUnitGroup, TimeSeries timeSeries)
        {
            return DecomposeIntegrationUnits(coreUnitGroup, TimeUnitGroup, timeSeries);
        }

        private (Unit CoreUnit, Unit IntegrationUnit) DecomposeIntegrationUnits(string coreUnitGroup, string integrationUnitGroup, TimeSeries timeSeries)
        {
            var parts = timeSeries.Unit.Split(TimeSeparatorChars, 2);

            if (parts.Length != 2)
                throw new ExpectedException($"The '{timeSeries.Unit}' unit of '{timeSeries.Identifier}' is not a supported {integrationUnitGroup}-based unit");

            var coreUnitId = parts[0];
            var integrationUnitId = parts[1];

            var coreUnit = GetUnit(coreUnitGroup, coreUnitId);
            var integrationUnit = GetUnit(integrationUnitGroup, integrationUnitId);

            return (coreUnit, integrationUnit);
        }

        private static readonly char[] TimeSeparatorChars = {'/'};

        private const string TimeUnitGroup = "Time";
        private const string VolumeUnitGroup = "Volume";
        private const string MassUnitGroup = "Mass";

        private Unit GetTimeUnit(string unitId)
        {
            return GetUnit(TimeUnitGroup, unitId);
        }

        private Unit GetVolumeUnit(string unitId)
        {
            return GetUnit(VolumeUnitGroup, unitId);
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

            var (volumeUnit, timeUnit) = DecomposeTimePeriodUnits(VolumeUnitGroup, dischargeSeries);

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
