using System.Collections.Generic;

namespace ManualGaugingPlugin.FileData
{
    public class Meter
    {
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string Manufacturer { get; set; }
        public List<MeterEquation> Equations { get; set; }
    }
}
