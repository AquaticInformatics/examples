using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class Sensor
    {
        public string OrganisationId { get; set; }
        public string Name { get; set; }
        public string Serial { get; set; }
        public string SensorType { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public DisplayInfo DisplayInfo { get; set; }
        public Config Config { get; set; }
        public LatestData LatestData { get; set; }
    }
}
