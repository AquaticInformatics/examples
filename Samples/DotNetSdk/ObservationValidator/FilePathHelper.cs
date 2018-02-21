using System;
using System.IO;
using System.Reflection;

namespace ObservationValidator
{
    public static class FilePathHelper
    {
        public static string GetFullPathOfFileInExecutableFolder(string fileName)
        {
            var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(exeDirectory))
            {
                throw new ArgumentException("Failed to get the directory of the executable.");
            }

            return Path.Combine(exeDirectory, fileName);
        }
    }
}
