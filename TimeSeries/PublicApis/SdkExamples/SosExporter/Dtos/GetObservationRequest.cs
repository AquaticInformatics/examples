using System;
using System.Collections.Generic;
using ServiceStack;

namespace SosExporter.Dtos
{
    [Route("/service", HttpMethods.Get)]
    public class GetObservationRequest40 : RequestBase, IReturn<GetObservationResponse40>
    {
        public GetObservationRequest40()
            : base("GetObservation")
        {
        }

        public string ObservedProperty { get; set; }
        public string FeatureOfInterest { get; set; }
        public string TemporalFilter { get; set; }
    }

    public class GetObservationResponse40 : ResponseBase
    {
        public List<Observation40> Observations { get; set; }
    }

    public class Observation40
    {
        public string Type { get; set; }
        public string Procedure { get; set; }
        public string Offering { get; set; }
        public string ObservableProperty { get; set; }
        // FeatureOfInterest (don't care right now)
        public List<DateTimeOffset> PhenomenonTime { get; set; }
        public DateTimeOffset ResultTime { get; set; }
        public ObservationResult40 Result { get; set; }
    }

    public class ObservationResult40
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

    [Route("/service", HttpMethods.Get)]
    public class GetObservationRequest44 : RequestBase, IReturn<GetObservationResponse44>
    {
        public GetObservationRequest44()
            : base("GetObservation")
        {
        }

        public string ObservedProperty { get; set; }
        public string FeatureOfInterest { get; set; }
        public string TemporalFilter { get; set; }
    }

    public class GetObservationResponse44 : ResponseBase
    {
        public List<Observation44> Observations { get; set; }
    }

    public class Observation44
    {
        public string Type { get; set; }
        public string Procedure { get; set; }
        public string ObservableProperty { get; set; }
        public string FeatureOfInterest { get; set; }
        public DateTimeOffset PhenomenonTime { get; set; }
        public DateTimeOffset ResultTime { get; set; }
        public ObservationResult44 Result { get; set; }
    }

    public class ObservationResult44
    {
        public string Uom { get; set; }
        public double? Value { get; set; }
    }
}
