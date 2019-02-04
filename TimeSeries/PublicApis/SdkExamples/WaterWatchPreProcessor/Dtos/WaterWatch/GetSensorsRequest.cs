using System.Collections.Generic;
using ServiceStack;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    [Route("/organisations/{OrganisationId}/sensors", HttpMethods.Get)]
    public class GetSensorsRequest : IReturn<IList<Sensor>>
    {
        public string OrganisationId { get; set; }
    }
}
