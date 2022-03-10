using System.Collections.Generic;
using System.Linq;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Humanizer;

namespace PointZilla.PointReaders
{
    public static class PointSummarizer
    {
        public static string Summarize(List<TimeSeriesPoint> points, string label = "point")
        {
            var pointExtents = points.Any()
                ? $" [{points.First().Time} to {points.Last().Time}]"
                : "";

            return $"{label.ToQuantity(points.Count)}{pointExtents}";
        }
    }
}
