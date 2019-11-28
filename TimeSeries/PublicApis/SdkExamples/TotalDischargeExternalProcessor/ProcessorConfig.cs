using System;

namespace TotalDischargeExternalProcessor
{
    public class ProcessorConfig
    {
        public string EventTimeSeries { get; set; }
        public string DischargeTimeSeries { get; set; }
        public string DischargeTotalTimeSeries { get; set; }
        public TimeSpan? MinimumEventDuration { get; set; }
    }
}
