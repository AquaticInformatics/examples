using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Humanizer;
using log4net;
using MoreLinq.Extensions;
using NodaTime;
using ServiceStack;

namespace SamplesObservationExporter
{
    public class Exporter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private ISamplesClient Client { get; set; }

        public void Export()
        {
            Validate();
            using (Client = Connect())
            {
                ExportAll();
            }
        }

        private void Validate()
        {
            ThrowIfEmpty(nameof(Context.ServerUrl), Context.ServerUrl);
            ThrowIfEmpty(nameof(Context.ApiToken), Context.ApiToken);

            if (string.IsNullOrWhiteSpace(Context.CsvOutputPath))
            {
                Context.CsvOutputPath = Path.Combine(ExeHelper.ExeDirectory, $"ExportedObservations-{DateTimeOffset.Now:yyyyMMddHHmmss}.csv");
            }

            if (!Context.Overwrite)
                ThrowIfFileExists(Context.CsvOutputPath);
        }

        private void ThrowIfEmpty(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            throw new ExpectedException($"The /{name}= option is required");
        }

        private void ThrowIfFileExists(string path)
        {
            if (!File.Exists(path))
                return;

            throw new ExpectedException($"Cannot overwrite existing file '{path}'. Use /{nameof(Context.Overwrite)}={true} to enable overwrite mode, or choose a different output file.");
        }

        private ISamplesClient Connect()
        {
            Log.Info($"{ExeHelper.ExeNameAndVersion} connecting to {Context.ServerUrl} ...");

            return SamplesClient.CreateConnectedClient(Context.ServerUrl, Context.ApiToken);
        }

        private void ExportAll()
        {
            var (request, summary) = BuildRequest();

            Log.Info($"Exporting observations {summary} ...");

            var response = Client.LazyGet<Observation, GetObservationsV2, SearchResultObservation>(request);

            var analyticalGroups = Client.Get(new GetAnalyticalGroups())
                .DomainObjects;

            var observedPropertiesByGroup = Context
                .AnalyticalGroupIds
                .Select((name, i) =>
                {
                    var group = analyticalGroups
                        .First(ag => ag.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

                    return new AnalyticalGroupInfo
                    {
                        Group = group,
                        GroupOrder = i,
                        Properties = group.AnalyticalGroupItems.Select(item => item.ObservedProperty.CustomId).ToList()
                    };
                })
                .ToList();

            Log.Info($"Fetching all {response.TotalCount} matching observations.");

            var stopwatch = Stopwatch.StartNew();

            var items = response
                .DomainObjects
                .Where(item => item.NumericResult != null)
                .ToList();

            Log.Info($"{items.Count} numeric observations loaded in {stopwatch.Elapsed.Humanize(2)}.");

            var resultColumns = items
                .Select(item =>
                {
                    var observedPropertyName = item.ObservedProperty.CustomId;
                    var groupInfo = observedPropertiesByGroup
                        .FirstOrDefault(gi => gi.Properties.Contains(observedPropertyName));

                    return new ResultColumn
                    {
                        ObservedPropertyId = item.ObservedProperty.CustomId,
                        Unit = item.ObservedProperty.DefaultUnit?.CustomId,
                        MethodId = item.AnalysisMethod?.MethodId,
                        AnalyticalGroup = groupInfo?.Group?.Name,
                        AnalyticalGroupOrder = groupInfo?.GroupOrder ?? 0,
                        ObservedPropertyOrder = groupInfo?.Properties?.IndexOf(observedPropertyName) ?? 0
                    };
                })
                .DistinctBy(rc => new{rc.ObservedPropertyId, rc.Unit, rc.MethodId, rc.AnalyticalGroup})
                .OrderBy(column => column.AnalyticalGroupOrder)
                .ThenBy(column => column.ObservedPropertyOrder)
                .ThenBy(column => column.ObservedPropertyId)
                .ThenBy(column => column.MethodId)
                .ThenBy(column => column.Unit)
                .ToList();

            if (Context.Overwrite && File.Exists(Context.CsvOutputPath))
            {
                Log.Warn($"Overwriting existing file '{Context.CsvOutputPath}'.");
            }

            var headers = new[]
                {
                    "Lab: Sample ID",
                    "Location ID",
                    "Observed Date",
                    $"Observed Time_UTC{Context.UtcOffset:m}",
                    "Latitude",
                    "Longitude",
                    "County",
                    "Medium",
                    "Location Groups",
                }
                .Concat(resultColumns.SelectMany(column => new[]
                {
                    EscapeCsvColumn($"{column.ObservedPropertyId}_Value"),
                    EscapeCsvColumn($"{column.ObservedPropertyId}_{DistinctColumnUnitLabel(resultColumns, column)}")
                }))
                .ToList();

            Log.Info($"Writing observations to '{Context.CsvOutputPath}' ...");

            using (var stream = File.OpenWrite(Context.CsvOutputPath))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                var headerRow = string.Join(",", headers);
                writer.WriteLine(headerRow);

                var utcOffset = Context.UtcOffset.ToTimeSpan();

                foreach (var group in items.GroupBy(item => new
                {
                    item.FieldVisit.StartTime,
                    item.LabResultDetails?.LabSampleId,
                    item.SamplingLocation.CustomId
                })
                    .OrderBy(g => g.Key.StartTime)
                    .ThenBy(g => g.Key.LabSampleId)
                    .ThenBy(g => g.Key.CustomId))
                {
                    var observations = group.ToList();
                    var first = observations.First();
                    var location = first.SamplingLocation;

                    if (!first.FieldVisit?.StartTime.HasValue ?? true)
                        throw new ExpectedException($"{"observation".ToQuantity(observations.Count)} at location '{location.CustomId}' have no timestamp.");

                    var time = first.FieldVisit.StartTime.Value.ToDateTimeOffset().Add(utcOffset);

                    var columns = new List<string>(headerRow.Length)
                    {
                        EscapeCsvColumn(first.LabResultDetails?.LabSampleId),
                        EscapeCsvColumn(location.CustomId),
                        $"{time:yyyy-MM-dd}",
                        $"{time:HH:mm:ss}",
                        location.Latitude,
                        location.Longitude,
                        EscapeCsvColumn(location.Address?.CountyCode),
                        EscapeCsvColumn(first.Medium.CustomId),
                        EscapeCsvColumn(string.Join(";", location.SamplingLocationGroups.Select(sg => sg.Name)))
                    };

                    do
                    {
                        var (row, remainingObservations) = WriteRow(resultColumns, observations, columns);

                        writer.WriteLine(row);

                        observations = remainingObservations;
                    } while (observations.Any());
                }
            }
        }

        private class AnalyticalGroupInfo
        {
            public AnalyticalGroup Group { get; set; }
            public int GroupOrder { get; set; }
            public List<string> Properties { get; set; }
        }

        private class ResultColumn
        {
            public string ObservedPropertyId { get; set; }
            public string Unit { get; set; }
            public string MethodId { get; set; }
            public string AnalyticalGroup { get; set; }
            public int AnalyticalGroupOrder { get; set; }
            public int ObservedPropertyOrder { get; set; }
        }

        private static string DistinctColumnUnitLabel(List<ResultColumn> resultColumns, ResultColumn column)
        {
            var propertiesWithSameUnit = resultColumns
                .Where(c => c.ObservedPropertyId == column.ObservedPropertyId && !string.IsNullOrEmpty(c.Unit) && c.Unit == column.Unit)
                .ToList();

            return propertiesWithSameUnit.Count > 1
                ? $"{column.Unit}_{column.MethodId}"
                : !string.IsNullOrEmpty(column.Unit)
                    ? column.Unit
                    : column.MethodId;
        }

        private static string EscapeCsvColumn(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var escapeIndex = text.IndexOfAny(EscapeCsvChars);

            if (escapeIndex < 0)
                return text;

            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private static readonly char[] EscapeCsvChars = {',', '"'};

        private (string Row, List<Observation> RemainingObservations) WriteRow(List<ResultColumn> resultColumns, List<Observation> observations, IReadOnlyList<string> commonColumns)
        {
            var remainingObservations = new List<Observation>();

            var columns = new List<string>(commonColumns.Count + resultColumns.Count);
            columns.AddRange(commonColumns);

            foreach (var resultColumn in resultColumns)
            {
                var exportedResults = observations
                    .Where(item => resultColumn.ObservedPropertyId == item.ObservedProperty.CustomId
                                   && resultColumn.Unit == item.ObservedProperty.DefaultUnit?.CustomId
                                   && resultColumn.MethodId == item.AnalysisMethod?.MethodId)
                    .Select(ConvertToExportedResult)
                    .ToList();

                var propertyObservations = exportedResults
                    .DistinctBy(er => new {er.Value, er.Unit})
                    .ToList();

                if (propertyObservations.Count == 0)
                {
                    columns.Add(string.Empty);
                    columns.Add(string.Empty);

                    continue;
                }

                if (propertyObservations.Count > 1)
                {
                    remainingObservations.AddRange(propertyObservations.Skip(1).Select(po => po.Observation));
                }

                var propertyObservation = propertyObservations.First();

                columns.Add($"{propertyObservation.Value}");
                columns.Add(EscapeCsvColumn(propertyObservation.Unit));
            }

            var row = string.Join(",", columns);

            return (row, remainingObservations);
        }

        private class ExportedResult
        {
            public Observation Observation { get; set; }
            public string Unit { get; set; }
            public double? Value { get; set; }
        }

        private ExportedResult ConvertToExportedResult(Observation observation)
        {
            var isNonDetect = observation.NumericResult.DetectionCondition == DetectionConditionType.NOT_DETECTED
                              && observation.NumericResult.MethodDetectionLevel != null;

            var unit = !isNonDetect
                ? observation.NumericResult.Quantity?.Unit.CustomId
                : $"< {observation.NumericResult.MethodDetectionLevel.Value} {observation.NumericResult.MethodDetectionLevel.Unit.CustomId}";

            var value = !isNonDetect
                ? observation.NumericResult.Quantity?.Value
                : null;

            return new ExportedResult
            {
                Observation = observation,
                Unit = unit,
                Value = value,
            };
        }

        private (GetObservationsV2 Request, string summary) BuildRequest()
        {
            var clauses = new List<string>();
            var builder = new StringBuilder();

            var request = new GetObservationsV2();

            if (Context.StartTime.HasValue)
            {
                clauses.Add($"after {Context.StartTime:O}");
                request.StartObservedTime = Instant.FromDateTimeOffset(Context.StartTime.Value);
            }

            if (Context.EndTime.HasValue)
            {
                clauses.Add($"before {Context.EndTime:O}");
                request.EndObservedTime = Instant.FromDateTimeOffset(Context.EndTime.Value);
            }

            if (clauses.Any())
            {
                builder.Append(string.Join(" matching ", clauses));
                clauses.Clear();
            }

            if (Context.LocationIds.Any())
            {
                request.SamplingLocationIds =
                    GetPaginatedItemIds<SamplingLocation, GetSamplingLocations, SearchResultSamplingLocation>(
                        "location",
                        clauses,
                        Context.LocationIds,
                        item => item.CustomId,
                        item => item.Id);
            }

            if (Context.AnalyticalGroupIds.Any())
            {
                request.AnalyticalGroupIds =
                    GetItemIds<AnalyticalGroup, GetAnalyticalGroups, SearchResultAnalyticalGroup>(
                        "analytical group",
                        clauses,
                        Context.AnalyticalGroupIds,
                        item => item.Name,
                        item => item.Id);
            }

            if (Context.ObservedPropertyIds.Any())
            {
                request.ObservedPropertyIds =
                    GetItemIds<ObservedProperty, GetObservedProperties, SearchResultObservedProperty>(
                        "observed property",
                        clauses,
                        Context.ObservedPropertyIds,
                        item => item.CustomId,
                        item => item.Id);
            }

            if (Context.ProjectIds.Any())
            {
                request.ProjectIds =
                    GetItemIds<Project, GetProjects, SearchResultProject>(
                        "project",
                        clauses,
                        Context.ProjectIds,
                        item => item.CustomId,
                        item => item.Id);
            }

            if (clauses.Any())
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append($"with {string.Join(" and ", clauses)}");
            }

            return (request, builder.ToString());
        }

        private List<string> GetPaginatedItemIds<TDomainObject, TRequest, TResponse>(string type, List<string> clauses, List<string> names, Func<TDomainObject,string> nameSelector, Func<TDomainObject,string> idSelector)
            where TRequest : IPaginatedRequest, IReturn<TResponse>, new()
            where TResponse : IPaginatedResponse<TDomainObject>
        {
            var response = Client.LazyGet<TDomainObject, TRequest, TResponse>(new TRequest());

            var items = response.DomainObjects.ToList();

            return MapNamesToIds(type, clauses, names, items, nameSelector, idSelector);
        }

        private List<string> GetItemIds<TDomainObject, TRequest, TResponse>(string type, List<string> clauses, List<string> names, Func<TDomainObject, string> nameSelector, Func<TDomainObject, string> idSelector)
            where TRequest : IReturn<TResponse>, new()
            where TResponse : IPaginatedResponse<TDomainObject>
        {
            var response = Client.Get(new TRequest());

            var items = response.DomainObjects.ToList();

            return MapNamesToIds(type, clauses, names, items, nameSelector, idSelector);
        }

        private List<string> MapNamesToIds<TDomainObject>(string type, List<string> clauses, List<string> names, List<TDomainObject> items, Func<TDomainObject, string> nameSelector, Func<TDomainObject, string> idSelector)
        {
            var unmatchedNames = names
                .Where(name => items.All(item => !nameSelector(item).Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                .Distinct()
                .ToList();

            if (unmatchedNames.Any())
                throw new ExpectedException($"{$"unknown {type}".ToQuantity(unmatchedNames.Count)}: {string.Join(", ", unmatchedNames)}");

            clauses.Add(names.Count == 1
                ? $"{type} '{names.First()}'"
                : $"{type.ToQuantity(names.Count)} in ({string.Join(", ", names)})");

            var nameSet = new HashSet<string>(names.Distinct(), StringComparer.InvariantCultureIgnoreCase);

            return items
                .Where(item => nameSet.Contains(nameSelector(item)))
                .Select(idSelector)
                .Distinct()
                .ToList();
        }
    }
}
