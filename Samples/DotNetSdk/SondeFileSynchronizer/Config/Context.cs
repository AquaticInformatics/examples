using System;
using System.Collections.Generic;

namespace SondeFileSynchronizer.Config
{
    public class Context
    {
        public Setting Setting { get; set; } = new Setting(new Dictionary<string, string>());

        public Dictionary<string, string> HeaderMap { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> HeaderPropertyIdMap { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> PropertyIdUnitMap { get; set; } = new Dictionary<string, string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Setting.SamplesApiBaseUrl) ||
                Setting.SamplesApiBaseUrl.IndexOf(ConfigNames.PlaceHolderYourAccount, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ConfigException("You must set a Samples API base url in the config file.");
            }

            if (string.IsNullOrWhiteSpace(Setting.SamplesAuthToken) ||
                Setting.SamplesAuthToken.IndexOf(ConfigNames.PlaceHolderYourToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ConfigException("You must specify a Samples API auth token in the config file.");
            }
        }
    }
}
