using System;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using SondeFileImporter.Config;
using SondeFileImporter.FileManagement;
using SondeFileImporter.ServiceClient;
using SondeFileImporter.Transform;

namespace SondeFileImporter.Importer
{
    public class SondeFileImporter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Context _context;
        private readonly SondeFileManager _fileMan;
        private readonly SondeFileConverter _converter;

        public SondeFileImporter(Context context)
        {
            _context = context;
            _fileMan = new SondeFileManager(_context);
            _converter = new SondeFileConverter(_context);
        }

        public void Synchronize()
        {
            var csvFiles = _fileMan.GetSondeCsvFiles();
            if (!csvFiles.Any())
            {
                Log.Info($"No sonde csv files in '{_fileMan.SondeFileFolder}'");
                return;
            }

            using (var client = new ImportClient(_context))
            {
                foreach (var fileInfo in csvFiles)
                {
                    try
                    {
                        SynchronizeOneFile(fileInfo, client);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to process sonde file '{fileInfo.FullName}'.{ex.Message}");
                    }
                }
            }
        }

        private void SynchronizeOneFile(FileInfo fileInfo, ImportClient client)
        {
            var fileSet = new FileInfoSet(fileInfo);

            fileSet.ProcessingSondeFile =_fileMan.MoveToProcessing(fileInfo);

            fileSet.ConvertedSamplesFile = _converter.ToSamplesObservationFile(fileSet.ProcessingSondeFile);
            Log.Info($"Transformed successfully:'{fileSet.ConvertedSamplesFile.Name}'");

            try
            {
                var dryRunResult = DryRunImport(fileSet, client);
                if (dryRunResult.HasErrors)
                    return;

                var importResult = RunImport(fileSet, client);

                if (importResult.HasErrors) 
                    return;

                _fileMan.MoveAllToSuccessNoThrow(fileSet.ProcessingSondeFile, fileSet.ConvertedSamplesFile);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to import '{fileSet.ProcessingSondeFile.Name}'. Error: {ex.Message}");
                _fileMan.MoveAllToFailedNoThrow(fileSet.ProcessingSondeFile, fileSet.ConvertedSamplesFile);
            }
        }

        private ImportResult DryRunImport(FileInfoSet fileSet, ImportClient client)
        {
            Log.Info($"Trying to import '{fileSet.ConvertedSamplesFile.Name}'...");

            var dryRunResult = GetImportResultWithAction(client,
                () => client.PostImportDryRunForStatusUrl(fileSet.ConvertedSamplesFile));

            if (!dryRunResult.HasErrors)
            {
                return dryRunResult;
            }

            ReportImportFailed(client, dryRunResult.ResultResponse, fileSet);

            return dryRunResult;
        }

        private ImportResult GetImportResultWithAction(ImportClient client, Func<string> importFunc)
        {
            var statusUrl = importFunc();

            var status = client.GetImportStatusUntilComplete(statusUrl);
            var response = client.GetResult(status.ResultUri.ToString());

            return new ImportResult {ImportStatus = status, ResultResponse = response};
        }

        private ImportResult RunImport(FileInfoSet fileSet, ImportClient client)
        {
            Log.Info($"Importing '{fileSet.ConvertedSamplesFile.Name}'...");

            var result = GetImportResultWithAction(client,
                () => client.PostImportForStatusUrl(fileSet.ConvertedSamplesFile));

            if (result.HasErrors)
            {
                ReportImportFailed(client, result.ResultResponse, fileSet);
                if(result.SuccessCount > 0 || result.UpdateCount > 0)
                {
                    Log.Info($"Partial success. '{fileSet.OriginalSondeFile.Name}': " +
                         $"imported {result.SuccessCount}, updated {result.UpdateCount}, failed {result.ErrorCount}.");
                }

                return result;
            }

            Log.Info($"'{fileSet.OriginalSondeFile.Name}': " +
                     $"imported {result.SuccessCount}, updated {result.UpdateCount}, failed {result.ErrorCount}.");

            return result;
        }

        private void ReportImportFailed(ImportClient client, ImportResultResponse result, FileInfoSet fileSet)
        {
            Log.Error($"'{fileSet.OriginalSondeFile.Name}': {result.errorCount} errors found.");
            var invalidCsvText = client.GetContentWithoutAuthorizationHeader(result.invalidRowsCsvUrl);

            var failedFileInfo = _fileMan.GetFailedSamplesFileInfo(fileSet.OriginalSondeFile);
            _fileMan.SaveToFailedFolder(invalidCsvText, failedFileInfo);

            Log.Info($"Errors saved to 'Failed' folder:'{failedFileInfo.Name}'");

            _fileMan.MoveAllToFailedNoThrow(fileSet.ProcessingSondeFile, fileSet.ConvertedSamplesFile);
        }
    }
}
