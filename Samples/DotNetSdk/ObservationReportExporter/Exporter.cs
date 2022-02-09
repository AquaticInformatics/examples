using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Humanizer;
using log4net;
using NodaTime;
using ObservationReportExporter.ExtraApis.Samples;
using ObservationReportExporter.ExtraApis.TimeSeries;
using ServiceStack;
using ApplyTagRequest = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.ApplyTagRequest;
using Attachment = Aquarius.TimeSeries.Client.ServiceModels.Publish.Attachment;
using GetTags = Aquarius.TimeSeries.Client.ServiceModels.Provisioning.GetTags;

namespace ObservationReportExporter
{
    public class Exporter
    {
        // ReSharper disable once PossibleNullReferenceException
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private ISamplesClient Samples { get; set; }
        private IAquariusClient TimeSeries { get; set; }
        private IServiceClient SiteVisit { get; set; }
        private DateTimeOffset ExportTime { get; set; }
        private List<ApplyTagRequest> AppliedTags { get; } = new List<ApplyTagRequest>();

        private int ExportedLocations { get; set; }
        private int ExportedObservations { get; set; }
        private int SkippedLocations { get; set; }
        private int Errors { get; set; }
        private int DeletedAttachments { get; set; }

        public void Run()
        {
            ExportTime = Context.ExportTime ?? DateTimeOffset.Now;

            var stopwatch = Stopwatch.StartNew();

            ValidateBeforeConnection();

            using (Samples = CreateConnectedSamplesClient())
            using (TimeSeries = CreateConnectedTimeSeriesClient())
            {
                ValidateOnceConnected();

                ExportAllLocations();
            }

            LogAction($"Exported {"observation".ToQuantity(ExportedObservations)} using '{ExportTemplate.CustomId}' template into {"location".ToQuantity(ExportedLocations)}, skipping {"unknown location".ToQuantity(SkippedLocations)}, deleting {"existing attachment".ToQuantity(DeletedAttachments)}, with {"detected error".ToQuantity(Errors)} in {stopwatch.Elapsed.Humanize()}.");
        }

        private void LogAction(string message)
        {
            var prefix = Context.DryRun ? "DRY-RUN: " : string.Empty;

            Log.Info($"{prefix}{message}");
        }

        private void ValidateBeforeConnection()
        {
            ThrowIfMissing(nameof(Context.SamplesServer), Context.SamplesServer);
            ThrowIfMissing(nameof(Context.SamplesApiToken), Context.SamplesApiToken);
            ThrowIfMissing(nameof(Context.TimeSeriesServer), Context.TimeSeriesServer);
            ThrowIfMissing(nameof(Context.TimeSeriesUsername), Context.TimeSeriesUsername);
            ThrowIfMissing(nameof(Context.TimeSeriesPassword), Context.TimeSeriesPassword);
            ThrowIfMissing(nameof(Context.ExportTemplateName), Context.ExportTemplateName);
            ThrowIfMissing(nameof(Context.AttachmentFilename), Context.AttachmentFilename);

            if (Context.EndTime < Context.StartTime)
                throw new ExpectedException($"/{nameof(Context.StartTime)} must be less than /{nameof(Context.EndTime)}");

            if (Context.LocationIds.Any() && Context.LocationGroupIds.Any())
                throw new ExpectedException($"You cannot mix /{nameof(Context.LocationIds).Singularize()}= and /{nameof(Context.LocationGroupIds).Singularize()}= options.");

            if (!Context.LocationIds.Any() && !Context.LocationGroupIds.Any())
                throw new ExpectedException($"You must specify at least one /{nameof(Context.LocationIds).Singularize()}= or /{nameof(Context.LocationGroupIds).Singularize()}= option.");

            if (Context.ObservedPropertyIds.Any() && Context.AnalyticalGroupIds.Any())
                throw new ExpectedException($"You cannot mix /{nameof(Context.ObservedPropertyIds).Singularize()}= and /{nameof(Context.AnalyticalGroupIds).Singularize()}= options.");

            if (!Context.ObservedPropertyIds.Any() && !Context.AnalyticalGroupIds.Any())
                throw new ExpectedException($"You must specify at least one /{nameof(Context.ObservedPropertyIds).Singularize()}= or /{nameof(Context.AnalyticalGroupIds).Singularize()}= option.");
        }

        private void ThrowIfMissing(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ExpectedException($"The /{name} option cannot be empty.");
        }

        private ISamplesClient CreateConnectedSamplesClient()
        {
            Log.Info($"{ExeHelper.ExeNameAndVersion} connecting to {Context.SamplesServer} ...");

            return SamplesClient.CreateConnectedClient(Context.SamplesServer, Context.SamplesApiToken);
        }

        private IAquariusClient CreateConnectedTimeSeriesClient()
        {
            Log.Info($"Connecting to {Context.TimeSeriesServer} ...");

            var client = AquariusClient.CreateConnectedClient(Context.TimeSeriesServer, Context.TimeSeriesUsername, Context.TimeSeriesPassword);

            SiteVisit = client.RegisterCustomClient(Aquarius.TimeSeries.Client.EndPoints.Root.EndPoint + "/apps/v1");

            Log.Info($"Connected to {Context.TimeSeriesServer} ({client.ServerVersion}) as {Context.TimeSeriesUsername}");

            return client;
        }

        private void ValidateOnceConnected()
        {
            ValidateSamplesConfiguration();
            ValidateTimeSeriesConfiguration();
        }

        private SpreadsheetTemplate ExportTemplate { get; set; }

        private Dictionary<string, string> TimeSeriesLocationAliases { get; } =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        private List<string> AnalyticalGroupIds { get; set; }
        private List<string> ObservedPropertyIds { get; set; }
        private List<string> SamplingLocationIds { get; set; }
        private List<string> SamplingLocationGroupIds { get; set; }

        private void ValidateSamplesConfiguration()
        {
            LoadExportTemplate();

            LoadExchangeConfiguration();

            var clauses = new List<string>();
            var builder = new StringBuilder();

            if (Context.StartTime.HasValue)
            {
                clauses.Add($"after {Context.StartTime:O}");
            }

            if (Context.EndTime.HasValue)
            {
                clauses.Add($"before {Context.EndTime:O}");
            }

            if (clauses.Any())
            {
                builder.Append(string.Join(" and ", clauses));
                clauses.Clear();
            }

            var locationClauses = new List<string>();
            var locationGroupClauses = new List<string>();
            var analyticalGroupClauses = new List<string>();
            var observedPropertyClauses = new List<string>();

            if (Context.LocationIds.Any())
            {
                Log.Info($"Resolving {"sampling location ID".ToQuantity(Context.LocationIds.Count)} ...");

                SamplingLocationIds =
                    GetSpecificPaginatedItemIds<SamplingLocation, GetSamplingLocations, SearchResultSamplingLocation>(
                        "location",
                        locationClauses,
                        Context.LocationIds,
                        item => item.CustomId,
                        item => item.Id,
                        name => new GetSamplingLocations { CustomId = name });
            }

            if (Context.LocationGroupIds.Any())
            {
                Log.Info($"Resolving {"sampling location group ID".ToQuantity(Context.LocationGroupIds.Count)} ...");

                SamplingLocationGroupIds =
                    GetItemIds<SamplingLocationGroup, GetSamplingLocationGroups, SearchResultSamplingLocationGroup>(
                        "location group",
                        locationGroupClauses,
                        Context.LocationGroupIds,
                        item => item.Name,
                        item => item.Id);
            }

            if (Context.AnalyticalGroupIds.Any())
            {
                Log.Info($"Resolving {"analytical group ID".ToQuantity(Context.AnalyticalGroupIds.Count)} ...");

                AnalyticalGroupIds =
                    GetItemIds<AnalyticalGroup, GetAnalyticalGroups, SearchResultAnalyticalGroup>(
                        "analytical group",
                        analyticalGroupClauses,
                        Context.AnalyticalGroupIds,
                        item => item.Name,
                        item => item.Id);
            }

            if (Context.ObservedPropertyIds.Any())
            {
                Log.Info($"Resolving {"observed property ID".ToQuantity(Context.ObservedPropertyIds.Count)} ...");

                ObservedPropertyIds =
                    GetItemIds<ObservedProperty, GetObservedProperties, SearchResultObservedProperty>(
                        "observed property",
                        observedPropertyClauses,
                        Context.ObservedPropertyIds,
                        item => item.CustomId,
                        item => item.Id);
            }

            clauses.AddRange(locationClauses);
            clauses.AddRange(locationGroupClauses);
            clauses.AddRange(analyticalGroupClauses);
            clauses.AddRange(observedPropertyClauses);

            if (clauses.Any())
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append($"from {string.Join(" and ", clauses)}");
            }

            var summary = builder.ToString();

            Log.Info($"Exporting observations {summary}.");
        }

        private void LoadExportTemplate()
        {
            ExportTemplate = Samples
                .Get(new GetSpreadsheetTemplates())
                .DomainObjects
                .FirstOrDefault(t =>
                    t.CustomId.Equals(Context.ExportTemplateName, StringComparison.InvariantCultureIgnoreCase) &&
                    t.Type == SpreadsheetTemplateType.OBSERVATION_EXPORT);

            if (ExportTemplate == null)
                throw new ExpectedException($"'{Context.ExportTemplateName}' is not a known Observation Export spreadsheet template");

            if (ExportTemplate.Attachments.Count != 1)
                throw new ExpectedException($"Export template '{ExportTemplate.CustomId}' should have a single attachment, but {ExportTemplate.Attachments.Count} were found.");
        }

        private void LoadExchangeConfiguration()
        {
            var exchangeConfiguration = Samples
                .Get(new GetExchangeConfigurations())
                .DomainObjects
                .FirstOrDefault(e => e.Type == "AQUARIUS_TIMESERIES");

            if (exchangeConfiguration == null)
                return;

            TimeSeriesLocationAliases.Clear();

            foreach (var mapping in exchangeConfiguration.SamplingLocationMappings)
            {
                if (mapping.SamplingLocation.CustomId.Equals(mapping.ExternalLocation, StringComparison.InvariantCultureIgnoreCase))
                    break;

                TimeSeriesLocationAliases.Add(mapping.SamplingLocation.CustomId, mapping.ExternalLocation);
            }
        }

        private List<string> GetSpecificPaginatedItemIds<TDomainObject, TRequest, TResponse>(string type, List<string> clauses, List<string> names, Func<TDomainObject, string> nameSelector, Func<TDomainObject, string> idSelector, Func<string, TRequest> requestFunc)
            where TRequest : IPaginatedRequest, IReturn<TResponse>, new()
            where TResponse : IPaginatedResponse<TDomainObject>
        {
            var items = names
                .SelectMany(name => Samples.LazyGet<TDomainObject, TRequest, TResponse>(requestFunc(name)).DomainObjects)
                .ToList();

            return MapNamesToIds(type, clauses, names, items, nameSelector, idSelector);
        }

        private List<string> GetItemIds<TDomainObject, TRequest, TResponse>(string type, List<string> clauses, List<string> names, Func<TDomainObject, string> nameSelector, Func<TDomainObject, string> idSelector)
            where TRequest : IReturn<TResponse>, new()
            where TResponse : IPaginatedResponse<TDomainObject>
        {
            var response = Samples.Get(new TRequest());

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


        private void ValidateTimeSeriesConfiguration()
        {
            GetAttachmentFilename("DummyLocation");

            if (!Context.AttachmentTags.Any())
                return;

            var locationTags = TimeSeries
                .Provisioning
                .Get(new GetTags())
                .Results
                .Where(t => t.AppliesToAttachments)
                .ToDictionary(t => t.Key, t => t, StringComparer.InvariantCultureIgnoreCase);

            AppliedTags.Clear();

            foreach (var kvp in Context.AttachmentTags)
            {
                if (!locationTags.TryGetValue(kvp.Key, out var locationTag))
                    throw new ExpectedException($"'{kvp.Key}' is not an existing tag with {nameof(Tag.AppliesToAttachments)}=true");

                AppliedTags.Add(new ApplyTagRequest
                {
                    UniqueId = locationTag.UniqueId,
                    Value = kvp.Value
                });
            }
        }

        private void ExportAllLocations()
        {
            var exportedLocations = GetExportedLocations()
                .OrderBy(l => l.CustomId)
                .ToList();

            Log.Info($"Exporting observations using the {ExportTemplate.CustomId} template for {"location".ToQuantity(exportedLocations.Count)} ...");

            foreach (var exportedLocation in exportedLocations)
            {
                try
                {
                    ExportLocation(exportedLocation);
                }
                catch (Exception e)
                {
                    ++Errors;
                    Log.Warn($"Skipping export of '{exportedLocation.CustomId}': {e.Message}");
                }
            }
        }

        private IEnumerable<SamplingLocation> GetExportedLocations()
        {
            if (SamplingLocationIds?.Any() ?? false)
                return SamplingLocationIds
                    .Select(id => Samples.Get(new GetSamplingLocation { Id = id }));

            return Samples
                .LazyGet<SamplingLocation, GetSamplingLocations, SearchResultSamplingLocation>(new GetSamplingLocations
                {
                    SamplingLocationGroupIds = SamplingLocationGroupIds
                })
                .DomainObjects;
        }

        private void ExportLocation(SamplingLocation location)
        {
            if (!TimeSeriesLocationAliases.TryGetValue(location.CustomId, out var aqtsLocationIdentifier))
                aqtsLocationIdentifier = location.CustomId;

            var locationDescriptions = TimeSeries.Publish.Get(new LocationDescriptionListServiceRequest
                {
                    LocationIdentifier = aqtsLocationIdentifier
                })
                .LocationDescriptions;

            if (!locationDescriptions.Any())
            {
                Log.Warn($"AQTS Location '{aqtsLocationIdentifier}' does not exist. Skipping this location's export.");
                ++SkippedLocations;
                return;
            }

            if (locationDescriptions.Count != 1)
                throw new ExpectedException(
                    $"'{aqtsLocationIdentifier}' is an ambiguous AQTS location identifier for {locationDescriptions.Count} locations: '{string.Join("', '", locationDescriptions.Select(l => l.Identifier))}'");

            var locationDescription = locationDescriptions.Single();

            var locationData = TimeSeries.Publish.Get(new LocationDataServiceRequest
            {
                LocationIdentifier = locationDescription.Identifier,
                IncludeLocationAttachments = true
            });

            var attachmentFilename = GetAttachmentFilename(locationDescription.Identifier);

            var existingAttachments = locationData
                .Attachments
                .Where(a => a.FileName.Equals(attachmentFilename, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            foreach (var existingAttachment in existingAttachments)
            {
                DeleteExistingAttachment(locationData, existingAttachment);
            }

            var exportRequest = new GetExportObservations
            {
                EndObservedTime = FromDateTimeOffset(Context.EndTime),
                StartObservedTime = FromDateTimeOffset(Context.StartTime),
                SamplingLocationIds = new List<string> { location.Id },
                ObservedPropertyIds = ObservedPropertyIds,
                AnalyticalGroupIds = AnalyticalGroupIds,
            };

            var exportedObservationCount = Samples.Get(new GetObservationsV2
            {
                EndObservedTime = exportRequest.EndObservedTime,
                StartObservedTime = exportRequest.StartObservedTime,
                SamplingLocationIds = exportRequest.SamplingLocationIds,
                ObservedPropertyIds = exportRequest.ObservedPropertyIds,
                AnalyticalGroupIds = exportRequest.AnalyticalGroupIds
            }).TotalCount;

            LogAction($"Exporting {"observation".ToQuantity(exportedObservationCount)} from '{location.CustomId}' ...");

            ++ExportedLocations;
            ExportedObservations += exportedObservationCount;

            // Need to hack the URL until WI-4928 is fixed
            var url = $"{(Samples.Client as JsonServiceClient)?.BaseUri}{exportRequest.ToGetUrl()}&observationTemplateAttachmentId={ExportTemplate.Attachments.Single().Id}&format=xlsx";

            if (Context.DryRun)
                return;

            var contentBytes = ExportObservationsFromTemplate(url);

            Log.Info($"Uploading '{attachmentFilename}' ({contentBytes.Length.Bytes().Humanize("#.#")}) to '{locationData.Identifier}' ...");

            UploadLocationAttachment(locationData.UniqueId, contentBytes, attachmentFilename);
        }

        private static Instant? FromDateTimeOffset(DateTimeOffset? dateTimeOffset)
        {
            return dateTimeOffset.HasValue
                ? (Instant?)Instant.FromDateTimeOffset(dateTimeOffset.Value)
                : null;
        }

        private byte[] ExportObservationsFromTemplate(string url)
        {
            using (var httpResponse = Samples.Client.Get<HttpWebResponse>(url))
            {
                return httpResponse.GetResponseStream().ReadFully();
            }
        }

        private void UploadLocationAttachment(Guid locationUniqueId, byte[] contentBytes, string filename)
        {
            using (var stream = new MemoryStream(contentBytes))
            {
                TimeSeries.Acquisition.PostFileWithRequest(stream, filename, new PostLocationAttachment
                {
                    LocationUniqueId = locationUniqueId,
                    Comments = $"Generated by {ExeHelper.ExeNameAndVersion}",
                    Tags = AppliedTags
                });
            }
        }

        private void DeleteExistingAttachment(LocationDataServiceResponse locationData, Attachment existingAttachment)
        {
            if (!Context.DeleteExistingAttachments)
                return;

            var match = AttachmentUrlRegex.Match(existingAttachment.Url);

            if (!match.Success)
                throw new ExpectedException($"Can't decode attachment ID from '{existingAttachment.Url}'");

            var attachmentId = match.Groups["attachmentId"].Value;

            ++DeletedAttachments;

            LogAction($"Deleting existing attachment '{existingAttachment.FileName}' (uploaded {existingAttachment.DateUploaded:O} from '{locationData.Identifier}' ...");

            if (Context.DryRun)
                return;

            SiteVisit.Delete(new DeleteAttachmentById { Id = attachmentId });
        }

        private static readonly Regex AttachmentUrlRegex = new Regex(@"apps/v1/attachments/(?<attachmentId>[^/]+)/download");

        private string GetAttachmentFilename(string locationIdentifier)
        {
            return FilenameGenerator.GenerateAttachmentFilename(Context.AttachmentFilename, ExportTemplate.CustomId, locationIdentifier, ExportTime);
        }
    }
}
