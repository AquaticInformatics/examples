using System;
using System.Collections.Generic;
using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/service", HttpMethods.Post)]
    public class GetCapabilitiesRequest : IReturn<GetCapabilitiesResponse>
    {
        public List<string> Sections { get; set; }
        public string Request { get; set; }
        public string Service { get; set; }
    }

    public class GetCapabilitiesResponse
    {
        public string Request { get; set; }
        public string Service { get; set; }
        public string Version { get; set; }
        public List<SensorInfo> Contents { get; set; }
    }

    public class SensorInfo
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
        public List<DateTimeOffset> PhenomenonTime { get; set; }
        public List<DateTimeOffset> ResultTime { get; set; }
        public List<string> Procedure { get; set; }
        public List<string> ObservableProperty { get; set; }
        public List<string> ResponseFormat { get; set; }
        public List<string> ObservationtType { get; set; }
        public List<string> FeatureOfInterestType { get; set; }
        public List<string> ProcedureDescriptionFormat { get; set; }
    }
}
