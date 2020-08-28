using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Aquarius.Helpers;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Humanizer;
using log4net;
using NodaTime;
using SamplesObservationExporter.PrivateApis;
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

            var client = SamplesClient.CreateConnectedClient(Context.ServerUrl, Context.ApiToken);

            var user = client.Get(new GetUserTokens()).User;

            var serverName = Context.ServerUrl;

            if (client.Client is SdkServiceClient sdkClient)
            {
                var serverUri = new Uri(sdkClient.BaseUri);

                serverName = $"{serverUri.Scheme}://{serverUri.Host}";
            }

            Log.Info($"Connected to {serverName} (v{client.ServerVersion}) as {user.UserProfile.FirstName}");

            return client;
        }


        private void ExportAll()
        {
            var (request, summary) = BuildRequest();

            Log.Info($"Exporting observations {summary} ...");

            var response = Client.LazyGet<Observation, GetObservationsV2Hacked, SearchResultObservation>(request);

            Log.Info($"Fetching all {response.TotalCount} matching observations.");

            var stopwatch = Stopwatch.StartNew();

            var items = response
                .DomainObjects
                .Where(item => item.NumericResult != null)
                .ToList();

            Log.Info($"{items.Count} numeric observations loaded in {stopwatch.Elapsed.Humanize(2)}.");

            var resultColumns = items
                .Select(item => (ObservedPropertyId: item.ObservedProperty.CustomId, Unit: item.ObservedProperty.DefaultUnit?.CustomId))
                .Distinct()
                .OrderBy(column => column.ObservedPropertyId)
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
                    EscapeCsvColumn($"{column.ObservedPropertyId}_{column.Unit}")
                }))
                .ToList();

            Log.Info($"Writing observations to '{Context.CsvOutputPath}' ...");

            using (var writer = new StreamWriter(Context.CsvOutputPath))
            {
                var headerRow = string.Join(", ", headers);
                writer.WriteLine(headerRow);

                var utcOffset = Context.UtcOffset.ToTimeSpan();

                foreach (var group in items.GroupBy(item => new
                {
                    item.ObservedTime,
                    item.LabResultDetails?.LabSampleId,
                    item.SamplingLocation.CustomId
                })
                    .OrderBy(g => g.Key.ObservedTime)
                    .ThenBy(g => g.Key.LabSampleId)
                    .ThenBy(g => g.Key.CustomId))
                {
                    var observations = group.ToList();
                    var first = observations.First();
                    var location = first.SamplingLocation;

                    if (!first.ObservedTime.HasValue)
                        throw new ExpectedException($"{"observation".ToQuantity(observations.Count)} at location '{location.CustomId}' have no timestamp.");

                    var time = first.ObservedTime.Value.ToDateTimeOffset().Add(utcOffset);

                    var columns = new List<string>(headerRow.Length)
                    {
                        EscapeCsvColumn(first.LabResultDetails?.LabSampleId),
                        EscapeCsvColumn(location.CustomId),
                        $"{time:yyyy-MM-dd}",
                        $"{time:HH:mm:ss.fff}",
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

        private (string Row, List<Observation> RemainingObservations) WriteRow(List<(string ObservedPropertyId, string Unit)> resultColumns, List<Observation> observations, IReadOnlyList<string> commonColumns)
        {
            var remainingObservations = new List<Observation>();

            var columns = new List<string>(commonColumns.Count + resultColumns.Count);
            columns.AddRange(commonColumns);

            foreach (var resultColumn in resultColumns)
            {
                var propertyObservations = observations
                    .Where(item => item.ObservedProperty.CustomId == resultColumn.ObservedPropertyId)
                    .Select(item => (
                        Observation: item,
                        Unit: item.NumericResult.Quantity?.Unit.CustomId,
                        // ReSharper disable once RedundantExplicitTupleComponentName
                        Value: item.NumericResult.Quantity?.Value,
                        NonDetect: item.NumericResult.LowerMethodReportingLimit != null && item.NumericResult.Quantity == null
                            ? $"< {item.NumericResult.LowerMethodReportingLimit.Value} {item.NumericResult.LowerMethodReportingLimit.Unit.CustomId}"
                            : null
                    ))
                    .Distinct()
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

                if (propertyObservation.Value.HasValue)
                {
                    columns.Add($"{propertyObservation.Value}");
                    columns.Add(EscapeCsvColumn(propertyObservation.Unit));

                    continue;
                }

                columns.Add(string.Empty);
                columns.Add(EscapeCsvColumn(propertyObservation.NonDetect));
            }

            var row = string.Join(", ", columns);

            return (row, remainingObservations);
        }

        private (GetObservationsV2Hacked Request, string summary) BuildRequest()
        {
            var clauses = new List<string>();
            var builder = new StringBuilder();

            var request = new GetObservationsV2Hacked();

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

        [DataContract]
        [Route("/v2/observations", "GET")]
        public class GetObservationsV2Hacked : IReturn<SearchResultObservation>, IPaginatedRequest
        {
            public string ActivityCustomId { get; set; }
            public List<string> ActivityIds { get; set; }
            public List<string> ActivityTypes { get; set; }
            public List<string> AnalysisMethodIds { get; set; }
            [DataMember(Name = "analyticalGroupIds")]
            public List<string> AnalyticalGroupIds { get; set; }
            public List<string> CollectionMethodIds { get; set; }
            [DataMember(Name = "cursor")]
            public string Cursor { get; set; }
            public string CustomId { get; set; }
            public List<string> DataClassifications { get; set; }
            public string DepthUnitCustomId { get; set; }
            public string DepthUnitId { get; set; }
            public double? DepthValue { get; set; }
            public DetectionConditionType? DetectionCondition { get; set; }
            [DataMember(Name = "end-observedTime")]
            public Instant? EndObservedTime { get; set; }
            public Instant? EndResultTime { get; set; }
            public Instant? EndModificationTime { get; set; }
            public FieldResultType? FieldResultType { get; set; }
            public string FieldVisitId { get; set; }
            public string FilterId { get; set; }
            public List<string> Ids { get; set; }
            public string ImportHistoryEventId { get; set; }
            public List<string> LabReportIds { get; set; }
            public List<string> LabResultLabAnalysisMethodIds { get; set; }
            public List<string> LabResultLaboratoryIds { get; set; }
            public int? Limit { get; set; }
            public List<string> Media { get; set; }
            [DataMember(Name = "observedPropertyIds")]
            public List<string> ObservedPropertyIds { get; set; }
            [DataMember(Name = "projectIds")]
            public List<string> ProjectIds { get; set; }
            public List<string> QualityControlTypes { get; set; }
            public List<string> ResultGrades { get; set; }
            public List<string> ResultStatuses { get; set; }
            public SampleFractionType? SampleFraction { get; set; }
            public List<string> SamplingContextTagIds { get; set; }
            public List<string> SamplingLocationGroupIds { get; set; }
            [DataMember(Name= "samplingLocationIds")]
            public List<string> SamplingLocationIds { get; set; }
            public List<string> Search { get; set; }
            public string Sort { get; set; }
            public List<string> SpecimenIds { get; set; }
            public string SpecimenName { get; set; }
            [DataMember(Name = "start-observedTime")]
            public Instant? StartObservedTime { get; set; }
            public Instant? StartResultTime { get; set; }
            public Instant? StartModificationTime { get; set; }
            public List<string> TaxonIds { get; set; }
        }


    }
}
