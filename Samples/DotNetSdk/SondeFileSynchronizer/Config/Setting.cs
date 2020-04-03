using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SondeFileSynchronizer.Config
{
    public class Setting
    {
        private static readonly string ExeFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string DefaultSondeFilePath = ExeFolderPath + @"\SondeFiles";
        private static readonly string DefaultResultsFolder = ExeFolderPath + @"\Results";
        private static readonly string DefaultArchiveFolder = ExeFolderPath + @"\Archives";

        public string FolderPathToMonitor => GetValueByNameOrDefault(ConfigNames.SondeFileFolder, DefaultSondeFilePath);
        public string ResultsFolder => GetValueByNameOrDefault(ConfigNames.ResultsFolder, DefaultResultsFolder);
        public string ArchiveFolder => GetValueByNameOrDefault(ConfigNames.ArchiveFolder, DefaultArchiveFolder);

        public string SamplesApiBaseUrl => GetValueByNameOrDefault(ConfigNames.SamplesApiBaseUrl, "");
        public string SamplesAuthToken => GetValueByNameOrDefault(ConfigNames.SamplesAuthToken, "");

        private readonly Dictionary<string, string> _nameValues;
        public Setting(Dictionary<string, string> nameValues)
        {
            _nameValues = nameValues;
        }

        private string GetValueByNameOrDefault(string name, string defaultValue)
        {
            if (_nameValues.TryGetValue(name, out string value))
            {
                return value;
            }

            return defaultValue;
        }
    }
}
