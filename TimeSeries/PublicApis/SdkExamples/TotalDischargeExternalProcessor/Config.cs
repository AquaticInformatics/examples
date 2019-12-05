using System;
using System.Collections.Generic;

namespace TotalDischargeExternalProcessor
{
    public class Config
    {
        public Defaults Defaults { get; set; } = new Defaults();
        public List<ProcessorConfig> Processors { get; set; } = new List<ProcessorConfig>();
    }

    public class Defaults
    {
        public string EventParameterAndLabel { get; set; } = "Count.Bottle Count";
        public string EventLabel { get; set; } = "Event";
        public string TotalLoadingPrefix { get; set; } = "Total ";
        public string SamplingLabel { get; set; } = "LabData";
        public string DischargeLabel { get; set; } = "Working";
        public string DischargeTotalUnit { get; set; }
        public string TotalLoadingUnit { get; set; }
        public TimeSpan MinimumEventDuration { get; set; } = TimeSpan.FromHours(2);
    }

    public class ProcessorConfig
    {
        public string Location { get; set; }
        public string EventTimeSeries { get; set; }
        public string DischargeTimeSeries { get; set; }
        public string DischargeTotalTimeSeries { get; set; }
        public string DischargeTotalUnit { get; set; }
        public TimeSpan? MinimumEventDuration { get; set; }
        public List<CalculationConfig> Calculations { get; set; } = new List<CalculationConfig>();
    }

    public class CalculationConfig
    {
        public string SamplingSeries { get; set; }
        public string TotalLoadingSeries { get; set; }
        public string TotalLoadingUnit { get; set; }
    }
}
