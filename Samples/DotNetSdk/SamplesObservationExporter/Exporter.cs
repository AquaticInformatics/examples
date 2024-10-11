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
            using (var progressReporter = new ProgressBarReporter())
            {
                ExportAll(progressReporter);
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

            if (Context.ProjectIds.Any() && Context.LocationIds.Any() && !Context.StartTime.HasValue && !Context.EndTime.HasValue)
            {
                throw new ExpectedException($"You must specify a /{nameof(Context.StartTime)}=date and/or /{nameof(Context.EndTime)}=date filter when filtering by both project and sampling locations.");
            }
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

        private void ExportAll(ProgressBarReporter progressBarReporter)
        {
            var analyticalGroups = Client.Get(new GetAnalyticalGroups())
                .DomainObjects;

            var (requests, summary) = BuildRequestsAndSummary(analyticalGroups);

            Log.Info($"Exporting observations {summary} ...");

            var responses = requests
                .Select(request => Client.LazyGet<Observation, GetObservationsV2, SearchResultObservation>(request, progressReporter: progressBarReporter))
                .ToList();

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

            progressBarReporter.ExpectedCount = responses.Sum(response => response.TotalCount);

            Log.Info($"Loading all {progressBarReporter.ExpectedCount} matching observations ...");

            var stopwatch = Stopwatch.StartNew();

            var items = responses
                .SelectMany(response => response
                    .DomainObjects
                    .Where(item => item.NumericResult != null)
                )
                .ToList();

            progressBarReporter.Dispose();

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

            Log.Info($"Exporting observations to '{Context.CsvOutputPath}' ...");

            var rowCount = 0;
            var observationCount = 0;

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

                    var time = first.FieldVisit.StartTime.Value.Add(utcOffset);

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
                        var (row, remainingObservations, writtenObservations) = WriteRow(resultColumns, observations, columns);

                        writer.WriteLine(row);

                        ++rowCount;
                        observationCount += writtenObservations;

                        observations = remainingObservations;
                    } while (observations.Any());
                }
            }

            Log.Info($"Exported {"distinct observation".ToQuantity(observationCount)} observations over {"row".ToQuantity(rowCount)} to '{Context.CsvOutputPath}'.");
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

        private (string Row, List<Observation> RemainingObservations, int WrittenObservations) WriteRow(List<ResultColumn> resultColumns, List<Observation> observations, IReadOnlyList<string> commonColumns)
        {
            var remainingObservations = new List<Observation>();

            var columns = new List<string>(commonColumns.Count + resultColumns.Count);
            columns.AddRange(commonColumns);

            var writtenObservations = 0;

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
                ++writtenObservations;
            }

            var row = string.Join(",", columns);

            return (row, remainingObservations, writtenObservations);
        }

        private class ExportedResult
        {
            public Observation Observation { get; set; }
            public string Unit { get; set; }
            public double? Value { get; set; }
        }

        private ExportedResult ConvertToExportedResult(Observation observation)
        {
            var isNonDetect = "NOT_DETECTED".Equals(observation.NumericResult.DetectionCondition?.SystemCode,
                                  StringComparison.InvariantCultureIgnoreCase)
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

        private (List<GetObservationsV2> Requests, string summary) BuildRequestsAndSummary(List<AnalyticalGroup> analyticalGroups)
        {
            var clauses = new List<string>();
            var builder = new StringBuilder();

            var startObservedTime = (Instant?) null;
            var endObservedTime = (Instant?)null;

            if (Context.StartTime.HasValue)
            {
                clauses.Add($"after {Context.StartTime:O}");
                startObservedTime = Instant.FromDateTimeOffset(Context.StartTime.Value);
            }

            if (Context.EndTime.HasValue)
            {
                clauses.Add($"before {Context.EndTime:O}");
                endObservedTime = Instant.FromDateTimeOffset(Context.EndTime.Value);
            }

            if (clauses.Any())
            {
                builder.Append(string.Join(" and ", clauses));
                clauses.Clear();
            }

            var analyticalGroupIds = new List<string>();
            var observedPropertyIds = new List<string>();
            var samplingLocationIds = new List<string>();
            var projectIds = new List<string>();

            if (Context.AnalyticalGroupIds.Any() || Context.ObservedPropertyIds.Any())
            {
                var analyticalGroupClauses = new List<string>();
                var observedPropertyClauses = new List<string>();

                if (Context.AnalyticalGroupIds.Any())
                {
                    analyticalGroupIds =
                        GetItemIds<AnalyticalGroup, GetAnalyticalGroups, SearchResultAnalyticalGroup>(
                            "analytical group",
                            analyticalGroupClauses,
                            Context.AnalyticalGroupIds,
                            item => item.Name,
                            item => item.Id);
                }

                if (Context.ObservedPropertyIds.Any())
                {
                    observedPropertyIds =
                        GetItemIds<ObservedProperty, GetObservedProperties, SearchResultObservedProperty>(
                            "observed property",
                            observedPropertyClauses,
                            Context.ObservedPropertyIds,
                            item => item.CustomId,
                            item => item.Id);
                }

                var namedAnalyticalGroups = analyticalGroupIds
                    .Select(id => analyticalGroups.First(analyticalGroup => analyticalGroup.Id == id))
                    .ToList();

                var allObservedPropertiesContainedInAnalyticalGroups = observedPropertyIds
                    .All(propertyId => namedAnalyticalGroups.Any(group =>
                        group.AnalyticalGroupItems.Any(item => item.ObservedProperty.Id == propertyId)));

                if (allObservedPropertiesContainedInAnalyticalGroups)
                {
                    // This will avoid one unnecessary lazy-load sequence
                    observedPropertyIds.Clear();
                    observedPropertyClauses.Clear();
                }

                if (analyticalGroupClauses.Any() && observedPropertyClauses.Any())
                {
                    clauses.Add($"({string.Join(" and ", analyticalGroupClauses)} or {string.Join(" and ", observedPropertyClauses)})");
                }
                else
                {
                    clauses.AddRange(analyticalGroupClauses.Any()
                        ? analyticalGroupClauses
                        : observedPropertyClauses);
                }
            }

            if (Context.LocationIds.Any() || Context.ProjectIds.Any())
            {
                var locationClauses = new List<string>();
                var projectClauses = new List<string>();

                if (Context.LocationIds.Any())
                {
                    samplingLocationIds =
                        GetPaginatedItemIds<SamplingLocation, GetSamplingLocations, SearchResultSamplingLocation>(
                            "location",
                            locationClauses,
                            Context.LocationIds,
                            item => item.CustomId,
                            item => item.Id);
                }

                if (Context.ProjectIds.Any())
                {
                    projectIds =
                        GetItemIds<Project, GetProjects, SearchResultProject>(
                            "project",
                            projectClauses,
                            Context.ProjectIds,
                            item => item.CustomId,
                            item => item.Id);
                }

                if (locationClauses.Any() && projectClauses.Any())
                {
                    clauses.Add($"({string.Join(" and ", locationClauses)} or {string.Join(" and ", projectClauses)})");
                }
                else
                {
                    clauses.AddRange(locationClauses.Any()
                        ? locationClauses
                        : projectClauses);
                }
            }

            if (clauses.Any())
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append($"with {string.Join(" and ", clauses)}");
            }

            var summary = builder.ToString();

            var requests = BuildRequests(
                startObservedTime,
                endObservedTime,
                analyticalGroupIds,
                observedPropertyIds,
                projectIds,
                samplingLocationIds);

            return (requests, summary);
        }

        private List<GetObservationsV2> BuildRequests(
            Instant? startObservedTime,
            Instant? endObservedTime,
            List<string> analyticalGroupIds,
            List<string> observedPropertyIds, 
            List<string> projectIds,
            List<string> samplingLocationIds)
        {
            var baseRequest = new GetObservationsV2
            {
                StartObservedTime = startObservedTime,
                EndObservedTime = endObservedTime,
            };

            var requests = new List<GetObservationsV2>();

            if (analyticalGroupIds.Any() || observedPropertyIds.Any())
            {
                if (analyticalGroupIds.Any() && observedPropertyIds.Any())
                {
                    CloneAndAddRequest(requests, baseRequest, request =>
                    {
                        request.AnalyticalGroupIds = analyticalGroupIds;
                        request.ObservedPropertyIds = observedPropertyIds;
                    });
                }

                if (analyticalGroupIds.Any())
                {
                    CloneAndAddRequest(requests, baseRequest, request => request.AnalyticalGroupIds = analyticalGroupIds);
                }

                if (observedPropertyIds.Any())
                {
                    CloneAndAddRequest(requests, baseRequest, request => request.ObservedPropertyIds = observedPropertyIds);
                }
            }

            if (!requests.Any())
            {
                // At this point, we can start with the base time-filtered request
                requests.Add(baseRequest);
            }

            if (projectIds.Any() || samplingLocationIds.Any())
            {
                var extraRequests = new List<GetObservationsV2>();

                if (projectIds.Any() && samplingLocationIds.Any())
                {
                    foreach (var existingRequest in requests)
                    {
                        CloneAndAddRequest(extraRequests, existingRequest, request =>
                        {
                            request.ProjectIds = projectIds;
                            request.SamplingLocationIds = samplingLocationIds;
                        });
                    }
                }

                if (projectIds.Any())
                {
                    foreach (var existingRequest in requests)
                    {
                        CloneAndAddRequest(extraRequests, existingRequest, request => request.ProjectIds = projectIds);
                    }
                }

                if (samplingLocationIds.Any())
                {
                    foreach (var existingRequest in requests)
                    {
                        CloneAndAddRequest(extraRequests, existingRequest, request => request.SamplingLocationIds = samplingLocationIds);
                    }
                }

                requests = extraRequests;
            }

            return requests;
        }

        private void CloneAndAddRequest(List<GetObservationsV2> requests, GetObservationsV2 request, Action<GetObservationsV2> action)
        {
            var clone = Clone(request);

            requests.Add(clone);

            action(clone);
        }

        private GetObservationsV2 Clone(GetObservationsV2 request)
        {
            return new GetObservationsV2
            {
                StartObservedTime = request.StartObservedTime,
                EndObservedTime = request.EndObservedTime,
                AnalyticalGroupIds = request.AnalyticalGroupIds,
                ObservedPropertyIds = request.ObservedPropertyIds,
                SamplingLocationIds = request.SamplingLocationIds,
                ProjectIds = request.ProjectIds,
            };
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
