using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Aquarius.Samples.Client;
using log4net;
using SondeFileSynchronizer.Config;
using SondeFileSynchronizer.FileManagement;
using SondeFileSynchronizer.SamplesDtos;
using SondeFileSynchronizer.Transform;

namespace SondeFileSynchronizer.Synchronize
{
    public class Synchronizer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Context _context;
        private readonly SondeFileManager _fileMan;
        private readonly SondeFileConverter _converter;

        public Synchronizer(Context context)
        {
            _context = context;
            _fileMan = new SondeFileManager(_context.Setting);
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

            using (var client =
                SamplesClient.CreateConnectedClient(_context.Setting.SamplesApiBaseUrl, _context.Setting.SamplesAuthToken))
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

        private void SynchronizeOneFile(FileInfo fileInfo, ISamplesClient client)
        {
            var processingFileInfo =_fileMan.MoveToProcessing(fileInfo);

            try
            {
                var convertedFileInfo = _converter.ToSamplesObservationFile(processingFileInfo);
                Log.Info($"Transformed successfully:'{convertedFileInfo.Name}'");

                //BillToDo: where can we get timeZoneOffset?
                var importRequest = new PostObservationImports { fileType = "SIMPLE_CSV", linkFieldVisitsForNewObservations = false, timeZoneOffset = "-08" };
                using (var stream = new MemoryStream(File.ReadAllBytes(convertedFileInfo.FullName)))
                {
                    //BillToDo: this is not working:
                    var response = client.PostFileWithRequest(stream, convertedFileInfo.Name, importRequest);
                }

                _fileMan.MoveToSuccess(processingFileInfo);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to import '{processingFileInfo.Name}'. Error: {ex.Message}");
                _fileMan.MoveToFailed(processingFileInfo);
            }
        }
    }
}
