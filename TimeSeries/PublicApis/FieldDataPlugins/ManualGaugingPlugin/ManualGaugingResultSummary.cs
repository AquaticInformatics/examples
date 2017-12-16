using System.Collections.Generic;
using System.Linq;
using FieldDataPluginFramework.DataModel.Verticals;

namespace ManualGaugingPlugin
{
    public class ManualGaugingResultSummary
    {
        public double TotalWidth { get; }
        public double TotalArea { get; }
        public double TotalDischarge { get; }
        public double MeanVelocity { get; }

        private readonly List<Vertical> _verticals;

        public ManualGaugingResultSummary(List<Vertical> verticals)
        {
            _verticals = verticals;

            TotalWidth = CalculateTotalWidth();
            TotalArea = CalculateTotalArea();
            TotalDischarge = CalculateTotalDischarge();
            MeanVelocity = CalculateMeanVelocity();
        }


        private double CalculateTotalWidth()
        {
            return _verticals.Where(v => v.Segment != null).Sum(v => v.Segment.Width);
        }

        private double CalculateTotalArea()
        {
            return _verticals.Where(v => v.Segment != null).Sum(v => v.Segment.Area);
        }

        private double CalculateTotalDischarge()
        {
            return _verticals.Where(v => v.Segment != null).Sum(v => v.Segment.Discharge);
        }

        private double CalculateMeanVelocity()
        {
            return TotalDischarge / TotalArea;
        }
    }
}
