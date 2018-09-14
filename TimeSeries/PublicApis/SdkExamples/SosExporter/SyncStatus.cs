using System;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Aquarius.TimeSeries.Client;
using CommunicationShared.Dto;
using log4net;
using ServiceStack;

namespace SosExporter
{
    public class SyncStatus
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string HostUrl { get; }

        public SyncStatus(IAquariusClient aquariusClient)
        {
            HostUrl = CreateHostUrl(aquariusClient);
        }

        private static string CreateHostUrl(IAquariusClient aquariusClient)
        {
            var publishUri = (aquariusClient.Publish as JsonServiceClient)?.BaseUri;

            if (string.IsNullOrEmpty(publishUri))
                return null;

            var uriBuilder = new UriBuilder(publishUri)
            {
                Path = "/AQUARIUS/AquariusDataService.svc"
            };

            return uriBuilder.ToString();
        }

        public Context Context { get; set; }

        private const string SosExporterGroup = "SosExporter";
        private const string HashKeySuffix = ".Hash";
        private const string ChangesSinceSuffix = ".ChangesSince";
        private const string CleanupEventGroup = "TimeSeriesEventLog";
        private const string CleanupEventHoursKey = "CleanupEventsOlderThan";

        public TimeSpan GetMaximumChangeEventDuration()
        {
            using (var client = CreateConnectedClient())
            {
                var cleanupEventsHours = client.GetGlobalSetting(CleanupEventGroup, CleanupEventHoursKey)?.SettingValue;

                return int.TryParse(cleanupEventsHours, out var hours)
                    ? TimeSpan.FromHours(hours)
                    : TimeSpan.FromDays(1);
            }
        }

        public DateTime? GetLastChangesSinceToken()
        {
            using (var client = CreateConnectedClient())
            {
                var savedConfigHash = client.GetGlobalSetting(SosExporterGroup, Context.ConfigurationName + HashKeySuffix)?.SettingValue;
                var savedChangesSince = client.GetGlobalSetting(SosExporterGroup, Context.ConfigurationName + ChangesSinceSuffix)?.SettingValue;

                if (string.IsNullOrEmpty(savedConfigHash) || string.IsNullOrEmpty(savedChangesSince))
                    return null;

                var currentConfigHash = ComputeConfigHash();

                if (savedConfigHash != currentConfigHash)
                {
                    Log.Warn($"Configuration change detected for '{Context.ConfigurationName}'. Performing full resync.");
                    return null;
                }

                Log.Info($"Restored previous export configuration from '{Context.ConfigurationName}' in {Context.Config.AquariusServer} global settings.");

                var dateTime = DateTime.ParseExact(savedChangesSince, "O", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                return dateTime;
            }
        }

        private ILegacyDataServiceClient CreateConnectedClient()
        {
            return LegacyDataServiceClient.Create(
                HostUrl ?? Context.Config.AquariusServer,
                Context.Config.AquariusUsername,
                Context.Config.AquariusPassword);
        }

        public void SaveConfiguration(DateTime nextChangesSinceToken)
        {
            using (var client = CreateConnectedClient())
            {
                var configHashSetting = CreateConfigurationGlobalSetting(HashKeySuffix, ComputeConfigHash());
                var changesSinceSetting = CreateConfigurationGlobalSetting(ChangesSinceSuffix, nextChangesSinceToken.ToString("O"));

                var summary = $"configuration to '{Context.ConfigurationName}' Next{ChangesSinceSuffix}={changesSinceSetting.SettingValue} in {Context.Config.AquariusServer} global settings.";

                if (Context.DryRun)
                {
                    Log.Warn($"Dry-run: Would have saved {summary}");
                    return;
                }

                client.SaveGlobalSetting(configHashSetting);
                client.SaveGlobalSetting(changesSinceSetting);

                Log.Info($"Saved {summary}");
            }
        }

        private GlobalSetting CreateConfigurationGlobalSetting(string settingKey, string settingValue)
        {
            return new GlobalSetting
            {
                LastModified = DateTime.UtcNow,
                SettingGroup = SosExporterGroup,
                SettingKey = Context.ConfigurationName + settingKey,
                SettingValue = settingValue
            };
        }

        private string ComputeConfigHash()
        {
            var configAsJson = Context.Config.ToJson();

            using (var sha = new SHA256Managed())
            {
                var bytes = Encoding.UTF8.GetBytes(configAsJson);
                var hash = sha.ComputeHash(bytes);

                return BitConverter
                    .ToString(hash)
                    .Replace("-", string.Empty);
            }
        }
    }
}
