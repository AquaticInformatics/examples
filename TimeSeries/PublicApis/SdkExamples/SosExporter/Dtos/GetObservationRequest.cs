using System;
using System.Collections.Generic;
using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/service", HttpMethods.Get)]
    public class GetObservationRequest : IReturn<GetObservationResponse>
    {
        public string Service { get; set; } = "SOS";
        public string Version { get; set; } = "2.0.0";
        public string Request { get; set; } = "GetObservation";
        public string ObservedProperty { get; set; }
        public string FeatureOfInterest { get; set; }
        public string TemporalFilter { get; set; }
    }

    public class GetObservationResponse
    {
        public string Request { get; set; }
        public string Version { get; set; }
        public string Service { get; set; }
        public List<Observation> Observations { get; set; }
    }

    public class Observation
    {
        public string Type { get; set; }
        public string Procedure { get; set; }
        public string Offering { get; set; }
        public string ObservableProperty { get; set; }
        // FeatureOfInterest (don't care right now)
        public List<DateTimeOffset> PhenomenonTime { get; set; }
        public DateTimeOffset ResultTime { get; set; }
        public ObservationResult Result { get; set; }
    }

    public class ObservationResult
    {
        public List<ObservationResultField> Fields { get; set; }
        public List<List<string>> Values { get; set; }
    }

    public class ObservationResultField
    {
        public string Name { get; set; }
        public string Definition { get; set; }
        public string Type { get; set; }
        public string Uom { get; set; }
    }
}
