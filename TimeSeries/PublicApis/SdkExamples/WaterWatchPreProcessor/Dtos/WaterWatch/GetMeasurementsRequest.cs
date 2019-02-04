using System;
using ServiceStack;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    [Route("/organisations/{OrganisationId}/sensors/{SensorSerial}/measurements", HttpMethods.Get)]
    public class GetMeasurementsRequest : IReturn<GetMeasurementsResponse>
    {
        public string OrganisationId { get; set; }
        public string SensorSerial { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Order { get; set; }
        public string Start { get; set; }
    }
}
