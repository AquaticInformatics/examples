namespace WaterWatchPreProcessor
{
    public class Context
    {
        public string WaterWaterOrgId { get; set; }
        public string WaterWaterApiKey { get; set; }
        public string WaterWaterApiToken { get; set; }
        public string SaveStatePath { get; set; } = "WaterWatchSaveState.json";
    }
}
