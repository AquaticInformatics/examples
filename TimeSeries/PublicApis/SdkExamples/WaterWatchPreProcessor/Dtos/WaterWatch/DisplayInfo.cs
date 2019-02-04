using System.Collections.Generic;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class DisplayInfo
    {
        public double? MaxLevel { get; set; }
        public double? MinLevel { get; set; }
        public double? OffsetMeasurement { get; set; }
        public IList<ReferenceLine> ReferenceLines { get; set; }
    }
}