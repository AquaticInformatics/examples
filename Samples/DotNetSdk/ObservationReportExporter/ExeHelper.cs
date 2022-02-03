using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ObservationReportExporter
{
    public class ExeHelper
    {
        // ReSharper disable once PossibleNullReferenceException
        public static string ExeFullPath => Path.GetFullPath(Assembly.GetEntryAssembly().Location);
        public static string ExeDirectory => Path.GetDirectoryName(ExeFullPath);
        public static string ExeVersion => FileVersionInfo.GetVersionInfo(ExeFullPath).FileVersion;
        public static string ExeName => Path.GetFileNameWithoutExtension(ExeFullPath);
        public static string ExeNameAndVersion => $"{ExeName} (v{ExeVersion})";
    }
}
