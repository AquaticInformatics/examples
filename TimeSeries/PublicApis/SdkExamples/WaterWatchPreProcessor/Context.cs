using System;
using System.Collections.Generic;
using WaterWatchPreProcessor.Filters;

namespace WaterWatchPreProcessor
{
    public class Context
    {
        public string WaterWatchOrgId { get; set; }
        public string WaterWatchApiKey { get; set; }
        public string WaterWatchApiToken { get; set; }
        public string SaveStatePath { get; set; } = "WaterWatchSaveState.json";
        public OutputMode OutputMode { get; set; }
        public List<RegexFilter> SensorSerialFilters { get; set; } = new List<RegexFilter>();
        public List<RegexFilter> SensorNameFilters { get; set; } = new List<RegexFilter>();
        public DateTime? SyncFromUtc { get; set; }
        public int NewSensorSyncDays { get; set; } = 5;
    }
}
