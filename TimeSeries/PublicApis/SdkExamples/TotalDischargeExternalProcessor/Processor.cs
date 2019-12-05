using System;
using System.Collections.Generic;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;

namespace TotalDischargeExternalProcessor
{
    public class Processor
    {
        public TimeSeries EventTimeSeries { get; set; }
        public TimeSeries DischargeTimeSeries { get; set; }
        public TimeSeries DischargeTotalTimeSeries { get; set; }
        public TimeSpan MinimumEventDuration { get; set; }
        public List<Calculation> Calculations { get; } = new List<Calculation>();
    }

    public class Calculation
    {
        public TimeSeries SamplingTimeSeries { get; set; }
        public TimeSeries EventTimeSeries { get; set; }
        public TimeSeries TotalLoadingTimeSeries { get; set; }
    }
}