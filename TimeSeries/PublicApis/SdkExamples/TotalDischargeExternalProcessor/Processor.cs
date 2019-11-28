using System;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;

namespace TotalDischargeExternalProcessor
{
    public class Processor
    {
        public TimeSeries EventTimeSeries { get; set; }
        public TimeSeries DischargeTimeSeries { get; set; }
        public TimeSeries DischargeTotalTimeSeries { get; set; }
        public TimeSpan MinimumEventDuration { get; set; }
    }
}