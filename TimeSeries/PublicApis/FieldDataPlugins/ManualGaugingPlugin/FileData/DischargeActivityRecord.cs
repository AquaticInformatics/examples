using System.Collections.Generic;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Verticals;

namespace ManualGaugingPlugin.FileData
{
    public class DischargeActivityRecord
    {
        public double StartGageHeight { get; set; }
        public double EndGageHeight { get; set; }
        public Meter Meter { get; set; }
        public PointVelocityObservationType ObservationMethodType { get; set; }
        public List<ManualGaugingRecord> ManualGaugings { get; set; }
    }
}
