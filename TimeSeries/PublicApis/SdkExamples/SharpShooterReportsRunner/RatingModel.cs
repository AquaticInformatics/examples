namespace SharpShooterReportsRunner
{
    public class RatingModel : DataSetBase
    {
        public string Identifier { get; set; }
        public string QueryFrom { get; set; }
        public string QueryTo { get; set; }
        public string StepSize { get; set; }
    }
}
