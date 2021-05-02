using System;
using System.Collections.Generic;
using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/service", HttpMethods.Get)]
    public class GetWml2ObservationRequest : RequestBase, IReturn<GetWml2ObservationResponse>
    {
        public GetWml2ObservationRequest()
            : base("GetObservation")
        {
        }

        public string ObservedProperty { get; set; }
        public string FeatureOfInterest { get; set; }
        public string TemporalFilter { get; set; }
    }

    public class GetWml2ObservationResponse : ResponseBase
    {
        public List<Wml2Observation> Observations { get; set; }
    }

    public class Wml2Observation
    {
        public string Type { get; set; }
        public string Procedure { get; set; }
        public string Offering { get; set; }
        public string ObservableProperty { get; set; }
        // FeatureOfInterest (don't care right now)
        public List<DateTimeOffset> PhenomenonTime { get; set; }
        public DateTimeOffset ResultTime { get; set; }
        public Wml2ObservationResult Result { get; set; }
    }

    public class Wml2ObservationResult
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
