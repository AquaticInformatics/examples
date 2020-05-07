using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using log4net;
using ServiceStack;

namespace SosExporter
{
    public class SyncStatus
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAquariusClient Client { get; }

        private Dictionary<string,Setting> Settings { get; }

        public SyncStatus(IAquariusClient aquariusClient)
        {
            Client = aquariusClient;

            Settings = Client.Provisioning.Get(new GetSettings())
                .Results
                .ToDictionary(
                    GetSettingId,
                    setting => setting);
        }

        private static string GetSettingId(Setting setting)
        {
            return GetSettingId(setting.Group, setting.Key);
        }

        private static string GetSettingId(string group, string key)
        {
            return $"{group}/{key}";
        }

        public Context Context { get; set; }

        private const string SosExporterGroup = "SosExporter";
        private const string HashKeySuffix = "-Hash";
        private const string ChangesSinceSuffix = "-ChangesSince";
        private const string CleanupEventGroup = "TimeSeriesEventLog";
        private const string CleanupEventHoursKey = "CleanupEventsOlderThan";

        private string GetGlobalSetting(string group, string key)
        {
            return !Settings.TryGetValue(GetSettingId(@group, key), out var setting)
                ? null
                : setting.Value;
        }

        private void SaveGlobalSetting(Setting setting)
        {
            var settingId = GetSettingId(setting);

            if (Settings.TryGetValue(settingId, out var existingSetting))
            {
                existingSetting.Value = setting.Value;

                Settings[settingId] = Client.Provisioning.Put(new PutSetting
                {
                    Group = existingSetting.Group,
                    Key = existingSetting.Key,
                    Value = existingSetting.Value,
                    Description = existingSetting.Description
                });

                return;
            }

            Settings[settingId] = Client.Provisioning.Post(new PostSetting
            {
                Group = setting.Group,
                Key = setting.Key,
                Value = setting.Value,
                Description = setting.Description
            });
        }

        public TimeSpan GetMaximumChangeEventDuration()
        {
            var cleanupEventsHours = GetGlobalSetting(CleanupEventGroup, CleanupEventHoursKey);

            return int.TryParse(cleanupEventsHours, out var hours)
                ? TimeSpan.FromHours(hours)
                : TimeSpan.FromDays(1);
        }

        public DateTime? GetLastChangesSinceToken()
        {
            var savedConfigHash = GetGlobalSetting(SosExporterGroup, Context.ConfigurationName + HashKeySuffix);
            var savedChangesSince = GetGlobalSetting(SosExporterGroup, Context.ConfigurationName + ChangesSinceSuffix);

            if (string.IsNullOrEmpty(savedConfigHash) || string.IsNullOrEmpty(savedChangesSince))
            {
                Log.Warn($"No configuration settings restored for '{Context.ConfigurationName}'. Performing full resync.");
                return null;
            }

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

        public void SaveConfiguration(DateTime nextChangesSinceToken)
        {
            var configHashSetting = CreateConfigurationGlobalSetting(HashKeySuffix, ComputeConfigHash());
            var changesSinceSetting = CreateConfigurationGlobalSetting(ChangesSinceSuffix, nextChangesSinceToken.ToString("O"));

            var summary = $"configuration to '{Context.ConfigurationName}' Next{ChangesSinceSuffix}={changesSinceSetting.Value} in {Context.Config.AquariusServer} global settings.";

            if (Context.DryRun)
            {
                Log.Warn($"Dry-run: Would have saved {summary}");
                return;
            }

            SaveGlobalSetting(configHashSetting);
            SaveGlobalSetting(changesSinceSetting);

            Log.Info($"Saved {summary}");
        }

        private Setting CreateConfigurationGlobalSetting(string settingKey, string settingValue)
        {
            return new Setting
            {
                Group = SosExporterGroup,
                Key = Context.ConfigurationName + settingKey,
                Value = settingValue,
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
