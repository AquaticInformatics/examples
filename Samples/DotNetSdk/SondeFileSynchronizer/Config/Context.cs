using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SondeFileSynchronizer.Config
{
    public class Context
    {
        public Dictionary<string, string> SettingNameValues = new Dictionary<string, string>();

        public Dictionary<string, string> HeaderMap { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> HeaderPropertyIdMap { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PropertyIdUnitMap { get; set; } = new Dictionary<string, string>();

        private static readonly string ExeFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string DefaultSondeFilePath = ExeFolderPath + @"\SondeFiles";
        private static readonly string DefaultArchiveFolder = ExeFolderPath + @"\Archives";

        public string FolderPathToMonitor => GetValueByNameOrDefault(ConfigNames.SondeFileFolder, DefaultSondeFilePath);
        public string ArchiveFolder => GetValueByNameOrDefault(ConfigNames.ArchiveFolder, DefaultArchiveFolder);
        public string FailedFolder => Path.Combine(FolderPathToMonitor, "Failed");
        public string SuccessFolder => Path.Combine(FolderPathToMonitor, "Success");
        public string ProcessingFolder => Path.Combine(FolderPathToMonitor, "Processing");

        public string SamplesApiBaseUrl => GetValueByNameOrDefault(ConfigNames.SamplesApiBaseUrl, "");
        public string SamplesAuthToken => GetValueByNameOrDefault(ConfigNames.SamplesAuthToken, "");
        public string DefaultUtcOffset => GetValueByNameOrDefault(ConfigNames.DefaultUtcOffset,"");

        public int ArchiveWhenFileNumberIsLargerThan =>
            int.Parse(GetValueByNameOrDefault(ConfigNames.ArchiveWhenFileNumberIsLargerThan, "200"));

        private string GetValueByNameOrDefault(string name, string defaultValue)
        {
            if (SettingNameValues.TryGetValue(name, out string value))
            {
                return value;
            }

            return defaultValue;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SamplesApiBaseUrl) ||
                SamplesApiBaseUrl.IndexOf(ConfigNames.PlaceHolderYourAccount, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ConfigException("You must set a Samples API base url in the config file.");
            }

            if (string.IsNullOrWhiteSpace(SamplesAuthToken) ||
                SamplesAuthToken.IndexOf(ConfigNames.PlaceHolderYourToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ConfigException("You must specify a Samples API auth token in the config file.");
            }

            if (string.IsNullOrWhiteSpace(DefaultUtcOffset) ||
                !TimeSpan.TryParse(DefaultUtcOffset, out _))
            {
                throw new ConfigException("You must specify the default utc offset for the observation time.");
            }

            var fileNumberStr = GetValueByNameOrDefault(ConfigNames.ArchiveWhenFileNumberIsLargerThan, "");

            if (string.IsNullOrWhiteSpace(fileNumberStr) ||
                !int.TryParse(fileNumberStr, out _))
            {
                throw new ConfigException($"'{ConfigNames.ArchiveWhenFileNumberIsLargerThan}' must be an integer.'{fileNumberStr}' is invalid.");
            }
        }
    }
}
