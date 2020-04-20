using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using ExcelDataReader;
using log4net;
using SondeFileImporter.Transform;

namespace SondeFileImporter.FileManagement
{
    public class FileHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static FileInfo MoveReplaceFile(FileInfo fileInfo, string targetFolderPath)
        {
            Directory.CreateDirectory(targetFolderPath);

            var targetFilePath = Path.Combine(targetFolderPath, fileInfo.Name);
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }

            fileInfo.MoveTo(targetFilePath);

            return new FileInfo(targetFilePath);
        }

        public static DataTable ParseDataTableFromEmbeddedCsvFile(string embeddedFilePath)
        {
            using(var stream = Assembly.GetCallingAssembly().GetManifestResourceStream(embeddedFilePath))
            {
                return ParseDataTableFromStream(stream);
            }
        }

        private static DataTable ParseDataTableFromStream(Stream stream)
        {
            var config = new ExcelDataSetConfiguration
            {
                ConfigureDataTable = tableReader => new ExcelDataTableConfiguration { UseHeaderRow = true }
            };

            using (var reader = ExcelReaderFactory.CreateCsvReader(stream))
            {
                var dataSet = reader.AsDataSet(config);
                return dataSet.Tables[0];
            }
        }

        public static DataTable ParseDataTableFromFileInfo(FileInfo fileInfo)
        {
            using (var stream = fileInfo.OpenRead())
            {
                return ParseDataTableFromStream(stream);
            }
        }

        public static void ForceWriteDataTableAsCsvFile(FileInfo targetFileInfo, DataTable dataTable)
        {
            var csvText = dataTable.ToCsv();
            ForceWriteToFile(targetFileInfo, csvText);
        }

        public static void ForceWriteToFile(FileInfo targetFileInfo, string csvText)
        {
            if (!string.IsNullOrWhiteSpace(targetFileInfo.DirectoryName))
            {
                Directory.CreateDirectory(targetFileInfo.DirectoryName);
            }

            File.WriteAllText(targetFileInfo.FullName, csvText);
        }

        public static List<string> DeleteFilesNoThrow(string[] files)
        {
            var deletedFiles = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedFiles.Add(file);
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to delete file '{file}'. Error:{e.Message}");
                }
            }

            return deletedFiles;
        }
    }
}
