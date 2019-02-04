using System;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class LatestData
    {
        public bool AlertAsserted { get; set; }
        public bool OfflineAsserted { get; set; }
        public DateTime LastSeen { get; set; }
        public Measurement LastMeasurement { get; set; }
        public int SignalLevel { get; set; }
        public double Battery { get; set; }
        public string LinkQuality { get; set; }
    }
}