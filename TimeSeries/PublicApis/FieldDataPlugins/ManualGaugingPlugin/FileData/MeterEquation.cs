namespace ManualGaugingPlugin.FileData
{
    public class MeterEquation
    {
        public double StartRange { get; private set; }
        public double EndRange { get; private set; }
        public double Intercept { get; private set; }
        public double Slope { get; private set; }

        public MeterEquation(double startRange, double endRange, double slope, double intercept)
        {
            StartRange = startRange;
            EndRange = endRange;
            Intercept = intercept;
            Slope = slope;
        }
    }
}
