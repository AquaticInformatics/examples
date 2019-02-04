using System.Collections.Generic;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class GetMeasurementsResponse
    {
        public IList<Measurement> Measurements { get; set; }
        public string Next { get; set; }
    }
}
