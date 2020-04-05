using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using SondeFileSynchronizer.Config;

namespace SondeFileSynchronizer.FileManagement
{
    public class SondeFileManager
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Setting _setting;

        public SondeFileManager(Setting setting)
        {
            _setting = setting;
        }

        public string SondeFileFolder => _setting.FolderPathToMonitor;

        public FileInfo MoveToProcessing(FileInfo fileInfo)
        {
            return MoveFileToFolder(fileInfo, _setting.ProcessingFolder);
        }

        private FileInfo MoveFileToFolder(FileInfo fileInfo, string targetFolderPath)
        {
            var movedTo = FileHelper.MoveReplaceFile(fileInfo, targetFolderPath);
            Log.Info($"Moved  to '{movedTo.FullName}'");

            return movedTo;
        }

        public FileInfo MoveToSuccess(FileInfo fileInfo)
        {
            return MoveFileToFolder(fileInfo, _setting.SuccessFolder);
        }

        public FileInfo MoveToFailed(FileInfo fileInfo)
        {
            return MoveFileToFolder(fileInfo, _setting.FailedFolder);
        }

        public List<FileInfo> GetSondeCsvFiles()
        {
            var dir = new DirectoryInfo(SondeFileFolder);
            if (!dir.Exists)
            {
                throw new ConfigException($"Folder to monitor does not exist:'{SondeFileFolder}'");
            }

            return dir.GetFiles("*.csv", SearchOption.TopDirectoryOnly).ToList();
        }

        public string GetConvertedSamplesFilePath(FileInfo fileInfo)
        {
            return Path.Combine(_setting.ConvertedFolder, fileInfo.Name + ".Samples.csv");
        }
    }
}
