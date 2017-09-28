using System.Collections.Generic;

namespace ManualGaugingPlugin.FileData
{
    public class ManualGaugingRecord
    {
        public double TaglinePosition { get; set; }
        public double SoundedDepth { get; set; }
        public List<VelocityObservation> Observations { get; set; }
    }
}
