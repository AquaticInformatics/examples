using System;
using System.Collections.Generic;
using CommunicationShared.Dto;

namespace SosExporter
{
    public interface ILegacyDataServiceClient : IDisposable
    {
        List<GlobalSetting> GetGlobalSettings(string settingGroup);
        GlobalSetting GetGlobalSetting(string settingGroup, string settingKey);
        void SaveGlobalSetting(GlobalSetting globalSetting);
        void DeleteGlobalSetting(GlobalSetting globalSetting);
    }
}
