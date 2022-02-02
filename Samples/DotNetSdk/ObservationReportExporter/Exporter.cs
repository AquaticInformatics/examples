using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Humanizer;
using log4net;
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

        public void Run()
        {
            ValidateBeforeConnection();

            using (Samples = CreateConnectedSamplesClient())
            using (TimeSeries = CreateConnectedTimeSeriesClient())
            {
                ValidateOnceConnected();
            }
        }

        private void ValidateBeforeConnection()
        {
            ThrowIfMissing(nameof(Context.SamplesServer), Context.SamplesServer);
            ThrowIfMissing(nameof(Context.SamplesApiToken), Context.SamplesApiToken);
            ThrowIfMissing(nameof(Context.TimeSeriesServer), Context.TimeSeriesServer);
            ThrowIfMissing(nameof(Context.TimeSeriesUsername), Context.TimeSeriesUsername);
            ThrowIfMissing(nameof(Context.TimeSeriesPassword), Context.TimeSeriesPassword);
            ThrowIfMissing(nameof(Context.ExportTemplateName), Context.ExportTemplateName);

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

            Log.Info($"Connected to {Context.TimeSeriesServer} ({client.ServerVersion}) as {Context.TimeSeriesUsername}");

            return client;
        }

        private void ValidateOnceConnected()
        {
            ValidateSamplesConfiguration();
            ValidateTimeSeriesConfiguration();
        }

        private SpreadsheetTemplate ExportTemplate { get; set; }
        private void ValidateSamplesConfiguration()
        {
            ExportTemplate = Samples
                    .Get(new GetSpreadsheetTemplates())
                    .DomainObjects
                    .FirstOrDefault(t => t.CustomId.Equals(Context.ExportTemplateName, StringComparison.InvariantCultureIgnoreCase));

            if (ExportTemplate == null)
                throw new ExpectedException($"'{Context.ExportTemplateName}' is not a known spreadsheet template");
        }

        private Dictionary<string, Tag> LocationTags { get; set; }

        private void ValidateTimeSeriesConfiguration()
        {
            if (!Context.AttachmentTags.Any())
                return;

            LocationTags = TimeSeries
                .Provisioning
                .Get(new GetTags())
                .Results
                .Where(t => t.AppliesToLocations)
                .ToDictionary(t => t.Key, t => t, StringComparer.InvariantCultureIgnoreCase);

            foreach (var kvp in Context.AttachmentTags)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (!LocationTags.TryGetValue(key, out var locationTag))
                    throw new ExpectedException($"'{key}' is not an existing tag with {nameof(locationTag.AppliesToLocations)}=true");
            }
        }

    }
}
