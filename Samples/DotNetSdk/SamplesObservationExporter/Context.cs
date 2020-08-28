using System;
using System.Collections.Generic;
using NodaTime;

namespace SamplesObservationExporter
{
    public class Context
    {
        public string ServerUrl { get; set; }
        public string ApiToken { get; set; }
        public string CsvOutputPath { get; set; }
        public bool Overwrite { get; set; }
        public Offset UtcOffset { get; set; } = Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks);
        public List<string> LocationIds { get; } = new List<string>();
        public List<string> AnalyticalGroupIds { get; } = new List<string>();
        public List<string> ObservedPropertyIds { get; } = new List<string>();
        public List<string> ProjectIds { get; } = new List<string>();
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
    }
}
