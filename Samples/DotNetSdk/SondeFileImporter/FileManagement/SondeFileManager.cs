using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using log4net;
using SondeFileImporter.Config;

namespace SondeFileImporter.FileManagement
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

        public void ArchiveSuccessFiles()
        {
            var successFolder = _context.SuccessFolder;
            if (!Directory.Exists(successFolder))
            {
                return;
            }

            var files = Directory.GetFiles(successFolder, "*.*");
            if (files.Length <= _context.ArchiveWhenFileNumberIsLargerThan)
            {
                return;
            }

            Log.Info($"Zipping up {files.Length} files in folder '{successFolder}'");
            
            var zipFilePath = Path.Combine(_context.ArchiveFolder, $"SuccessFiles_{DateTime.Now:yyyy-MM-dd_HH-mm}.zip");
            Directory.CreateDirectory(_context.ArchiveFolder);

            ZipDirectoryOrThrow(successFolder, zipFilePath);

            var deletedFiles= FileHelper.DeleteFilesNoThrow(files);
            Log.Info($"{deletedFiles.Count} files deleted from folder '{successFolder}'");

            var fileNumNotDeleted = files.Length - deletedFiles.Count;
            if (fileNumNotDeleted > 0)
            {
                Log.Warn($"{fileNumNotDeleted} files were not deleted. See the log file for reasons.");
            }
        }

        private static void ZipDirectoryOrThrow(string successFolder, string zipFilePath)
        {
            try
            {
                ZipFile.CreateFromDirectory(successFolder, zipFilePath);
                Log.Info($"Zip file:'{zipFilePath}'");
            }
            catch
            {
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                throw;
            }
        }
    }
}
