using System.IO;

namespace SondeFileSynchronizer.FileManagement
{
    public class FileHelper
    {
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
    }
}
