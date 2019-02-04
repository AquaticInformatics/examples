using System;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class Measurement
    {
        public DateTime Time { get; set; }
        public double RawDistance { get; set; }
    }
}