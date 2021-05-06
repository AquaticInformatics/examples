namespace SharpShooterReportsRunner
{
    public class TimeSeries
    {
        public string Identifier { get; set; }
        public string OutputUnitId { get; set; }
        public string QueryFrom { get; set; }
        public string QueryTo { get; set; }
        public GroupBy? GroupBy { get; set; }
    }
}
