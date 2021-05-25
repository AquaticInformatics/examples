using System.Collections.Generic;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;

namespace PointZilla.PointReaders
{
    public interface IPointReader
    {
        List<TimeSeriesPoint> LoadPoints();
    }
}
