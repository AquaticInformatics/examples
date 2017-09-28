namespace StageDischargePlugin.FileData
{
    public class DischargeActivityRecord
    {
        public string MeasurementId { get; set; }
        public double StartGageHeight { get; set; }
        public double EndGageHeight { get; set; }
        public double Discharge { get; set; }
        public string Party { get; set; }
        public string Comments { get; set; }
    }
}
