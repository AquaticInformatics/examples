using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReflectedSeriesAggregator
{
    public class WorkItem
    {
        public string Tag { get; set; }
        public Parameter Parameter { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public string TargetLocationIdentifier { get; set; }
        public string TargetLabel { get; set; }
        public List<TimeSeriesDescription> GroupedTimeSeriesSourceDescriptions { get; set; } = new List<TimeSeriesDescription>();
        public bool Publish { get; set; }
        public string TargetSeriesIdentifier { get => $"{Parameter.DisplayName}.{TargetLabel}@{TargetLocationIdentifier}"; }
        public override string ToString() => $"Tag:'{Tag}' ParameterId:'{Parameter.DisplayName}' Label:'{string.Join("|", Labels)}' Series to aggregate:{GroupedTimeSeriesSourceDescriptions.Count} Target series:'{TargetSeriesIdentifier}'";
        public string ToFullString()
        {
            string fullString = this.ToString();
            if (GroupedTimeSeriesSourceDescriptions.Count > 0)
            {
                fullString += Environment.NewLine;
                fullString += string.Join(Environment.NewLine, GroupedTimeSeriesSourceDescriptions.Select(t => $"   {t.Parameter}.{t.Label}@{t.LocationIdentifier}"));
            }
            return fullString;
        }
    }
}
