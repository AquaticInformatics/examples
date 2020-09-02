using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Humanizer;
using log4net;

namespace LabFileImporter
{
    public class Importer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public void Import()
        {
            Validate();

            var observations = LoadAll()
                .ToList();

            WriteObservationsAsCsv(observations);

            if (Context.MaximumObservations.HasValue)
            {
                observations = observations
                    .Take(Context.MaximumObservations.Value)
                    .ToList();
            }

            ImportObservationsToSamples(observations);
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(Context.ServerUrl) ^ string.IsNullOrWhiteSpace(Context.ApiToken))
                throw new ExpectedException(
                    $"You must specify both /{nameof(Context.ServerUrl)}= and /{nameof(Context.ApiToken)}= options to authenticate with AQUARIUS Samples");

            if (string.IsNullOrWhiteSpace(Context.ServerUrl) && string.IsNullOrWhiteSpace(Context.ApiToken) &&
                string.IsNullOrWhiteSpace(Context.CsvOutputPath))
                Context.CsvOutputPath = Path.Combine(ExeHelper.ExeDirectory,
                    $"Observations-{DateTimeOffset.Now:yyyyMMddHHmmss}.csv");

            if (!Context.Files.Any())
                throw new ExpectedException(
                    $"No files to import. Try setting a /{nameof(Context.Files).Singularize()}= option.");

            if (!Context.Overwrite && File.Exists(Context.CsvOutputPath))
                throw new ExpectedException(
                    $"Can't overwrite existing file '{Context.CsvOutputPath}'. Try /{nameof(Context.Overwrite)}={true}");
        }

        private IEnumerable<ObservationV2> LoadAll()
        {
            return Context
                .Files
                .SelectMany(LoadAllObservations);
        }

        private IEnumerable<ObservationV2> LoadAllObservations(string path)
        {
            var observations = new LabFileLoader
                {
                    Context = Context
                }
                .Load(path)
                .ToList();

            Log.Info($"Loaded {"observation".ToQuantity(observations.Count)} from '{path}'.");

            return observations;
        }

        private void WriteObservationsAsCsv(List<ObservationV2> observations)
        {
            if (string.IsNullOrEmpty(Context.CsvOutputPath))
                return;

            Log.Info($"Writing {"observation".ToQuantity(observations.Count)} to '{Context.CsvOutputPath}' ...");

            using (var writer = new StreamWriter(Context.CsvOutputPath))
            {
                new CsvWriter()
                    .WriteObservations(writer, observations);
            }
        }

        private void ImportObservationsToSamples(List<ObservationV2> observations)
        {
            if (string.IsNullOrWhiteSpace(Context.ServerUrl) || string.IsNullOrWhiteSpace(Context.ApiToken))
                return;

            Log.Info($"Connecting to {Context.ServerUrl} ...");

            using (var importClient = new ImportClient(Context))
            {
                var csvBytes = LoadObservationCsvBytes(observations);

                var filename = $"{ExeHelper.ExeNameAndVersion} Uploads.csv";

                Log.Info(Context.DryRun
                ? $"Dry-run of importing {"observation".ToQuantity(observations.Count)} ..."
                : $"Importing {"observation".ToQuantity(observations.Count)} ...");

                var stopwatch = Stopwatch.StartNew();

                var statusUrl = Context.DryRun
                    ? importClient.PostImportDryRunForStatusUrl(filename, csvBytes)
                    : importClient.PostImportForStatusUrl(filename, csvBytes);

                var status = importClient.GetImportStatusUntilComplete(statusUrl);
                var response = importClient.GetResult(status.ResultUri.ToString());

                var errors = response
                    .ErrorImportItems
                    ?.SelectMany(errorItem => errorItem
                        .Errors
                        .SelectMany(errorContext => errorContext.Value.Select(error =>
                            $"Row {errorItem.RowId}: {error.ErrorMessage} '{error.ErrorFieldValue}' [{errorContext.Key}]")))
                    .ToList();

                var summaryMessages = response
                    .SummaryReportText
                    .Split('\n')
                    .Concat(response.ImportJobErrors?.Select(e => e.ErrorMessage) ?? new string[0])
                    .Concat(errors?.Count > Context.ErrorLimit ? new[] {$"Showing first {Context.ErrorLimit} of {errors.Count} errors."} : new string[0])
                    .Concat(errors?.Take(Context.ErrorLimit) ?? new string[0])
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (response.ErrorCount > 0)
                {
                    Log.Error($"Import completed with errors in {stopwatch.Elapsed.Humanize(2)}.");
                    summaryMessages.ForEach(Log.Error);
                }
                else
                {
                    Log.Info($"Import completed successfully in {stopwatch.Elapsed.Humanize(2)}.");
                    summaryMessages.ForEach(Log.Info);
                }
            }
        }
        
        private byte[] LoadObservationCsvBytes(List<ObservationV2> observations)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream))
                {
                    new CsvWriter()
                        .WriteObservations(writer, observations);
                }

                return memoryStream.GetBuffer();
            }
        }
    }
}
