using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.Webclient;
using CommunicationShared;
using CommunicationShared.Dto;
using log4net;

namespace SosExporter
{
    public class LegacyDataServiceClient : ILegacyDataServiceClient
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRemoteDataService _aqServiceClient;
        private readonly string _hostName;
        private readonly string _loginUserName;

        public static ILegacyDataServiceClient Create(string hostName, string loginUserName, string password)
        {
            return new LegacyDataServiceClient(hostName, loginUserName, password);
        }

        private LegacyDataServiceClient(string hostName, string loginUserName, string password)
        {
            ThrowIfNotSpecified(hostName, nameof(hostName));
            ThrowIfNotSpecified(loginUserName, nameof(loginUserName));
            ThrowIfNotSpecified(password, nameof(password));

            _hostName = hostName;
            _loginUserName = loginUserName;

            CreateConnectedAquariusDataServiceClient(password);
        }


        private void ThrowIfNotSpecified(string stringValue, string paramName)
        {
            if (!string.IsNullOrWhiteSpace(stringValue))
                return;

            throw new ArgumentNullException($"{paramName} is null or empty.");
        }

        private void CreateConnectedAquariusDataServiceClient(string password)
        {
            _aqServiceClient = AQWSFactory.NewADSClient(_hostName, _loginUserName, password);
        }

        public void Dispose()
        {
            using (_aqServiceClient) { }
        }

        public List<GlobalSetting> GetGlobalSettings(string settingGroup)
        {
            return _aqServiceClient.GetGlobalSettingsForGroup(settingGroup, null);
        }

        public GlobalSetting GetGlobalSetting(string settingGroup, string settingKey)
        {
            var groupSettings = _aqServiceClient.GetGlobalSettingsForGroup(settingGroup, settingKey);

            return groupSettings.SingleOrDefault();
        }

        public void SaveGlobalSetting(GlobalSetting globalSetting)
        {
            var existingGlobalSetting = GetGlobalSetting(globalSetting.SettingGroup, globalSetting.SettingKey);

            if (existingGlobalSetting == null)
            {
                _aqServiceClient.InsertGlobalSetting(globalSetting);
            }
            else
            {
                _aqServiceClient.UpdateGlobalSetting(globalSetting);
            }
        }

        public void DeleteGlobalSetting(GlobalSetting globalSetting)
        {
            _aqServiceClient.DeleteGlobalSetting(globalSetting);
        }
    }
}
