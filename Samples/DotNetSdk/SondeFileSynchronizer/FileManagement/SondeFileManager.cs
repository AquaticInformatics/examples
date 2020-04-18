using System;
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

        private readonly Context _context;

        public SondeFileManager(Context context)
        {
            _context = context;
        }

        public string SondeFileFolder => _context.FolderPathToMonitor;

        public FileInfo MoveToProcessing(FileInfo fileInfo)
        {
            return MoveFileToFolder(fileInfo, _context.ProcessingFolder);
        }

        private FileInfo MoveFileToFolder(FileInfo fileInfo, string targetFolderPath)
        {
            var movedTo = FileHelper.MoveReplaceFile(fileInfo, targetFolderPath);
            Log.Debug($"'{fileInfo.Name}' moved to '{targetFolderPath}'");

            return movedTo;
        }

        public void MoveAllToFailedNoThrow(params FileInfo[] fileInfos)
        {
            MoveFilesToFolderNoThrow(fileInfos, _context.FailedFolder);
        }

        private void MoveFilesToFolderNoThrow(FileInfo[] fileInfos, string targetFolderPath)
        {
            foreach (var fileInfo in fileInfos)
            {
                if (!fileInfo.Exists)
                {
                    continue;
                }

                try
                {
                    MoveFileToFolder(fileInfo, targetFolderPath);
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error moving file '{fileInfo.FullName}' to folder '{targetFolderPath}'.", ex);
                }
            }
        }

        public void MoveAllToSuccessNoThrow(params FileInfo[] fileInfos)
        {
            MoveFilesToFolderNoThrow(fileInfos, _context.SuccessFolder);
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
            return Path.Combine(_context.ProcessingFolder, GetSamplesFileName(fileInfo));
        }

        private static string GetSamplesFileName(FileInfo fileInfo)
        {
            return Path.Combine(Path.GetFileNameWithoutExtension(fileInfo.Name) + ".Converted.csv");
        }

        public FileInfo GetFailedSamplesFileInfo(FileInfo fileInfo)
        {
            return new FileInfo(Path.Combine(_context.FailedFolder, 
                Path.GetFileNameWithoutExtension(GetSamplesFileName(fileInfo)) + "_Errors.csv"));
        }

        public void SaveToFailedFolder(string text, FileInfo fileInfo)
        {
            FileHelper.ForceWriteToFile(fileInfo, text);
        }
    }
}
