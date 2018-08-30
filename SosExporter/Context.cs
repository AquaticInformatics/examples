using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;

namespace SosExporter
{
    public class Config
    {
        // Changes to the AQTS or SOS credentials should force a resync
        public string AquariusServer { get; set; }
        public string AquariusUsername { get; set; } = "admin";
        public string AquariusPassword { get; set; } = "admin";
        public string SosServer { get; set; }
        public string SosUsername { get; set; }
        public string SosPassword { get; set; }

        // All the changes-since filtering options
        public string LocationIdentifier { get; set; }
        public string Parameter { get; set; }
        public bool? Publish { get; set; } = true;
        public string ComputationIdentifier { get; set; }
        public string ComputationPeriodIdentifier { get; set; }
        public ChangeEventType? ChangeEventType { get; set; }
        public List<ExtendedAttributeFilter> ExtendedFilters { get; set; } = new List<ExtendedAttributeFilter>();

        // Extra aggressive filtering options
        public List<TimeSeriesFilter> TimeSeries { get; } = new List<TimeSeriesFilter>();
        public List<ApprovalFilter> Approvals { get; } = new List<ApprovalFilter>();
        public List<GradeFilter> Grades { get; } = new List<GradeFilter>();
        public List<QualifierFilter> Qualifiers { get; } = new List<QualifierFilter>();

        public Dictionary<ComputationPeriod, int> MaximumPointDays { get; } = new Dictionary<ComputationPeriod, int>
        {
            {ComputationPeriod.Unknown, 90},
            {ComputationPeriod.Annual, -1},
            {ComputationPeriod.Monthly, -1},
            {ComputationPeriod.Weekly, 3653},
            {ComputationPeriod.Daily, 3653},
            {ComputationPeriod.Hourly, 365},
            {ComputationPeriod.Points, 30},
            //{ComputationPeriod.WaterYear, -1}, // These are treated like Annual
            {ComputationPeriod.Minutes, 30},
        };
    }

    public interface IFilter
    {
        bool Exclude { get; set; }
    }

    public class TimeSeriesFilter : IFilter
    {
        public bool Exclude { get; set; }
        public Regex Regex { get; set; }
    }

    public class ApprovalFilter : IFilter
    {
        public bool Exclude { get; set; }
        public string Text { get; set; }
        public int ApprovalLevel { get; set; }
        public ComparisonType ComparisonType { get; set; }
    }

    public class GradeFilter : IFilter
    {
        public bool Exclude { get; set; }
        public string Text { get; set; }
        public int GradeCode { get; set; }
        public ComparisonType ComparisonType { get; set; }
    }

    public class QualifierFilter : IFilter
    {
        public bool Exclude { get; set; }
        public string Text { get; set; }
    }

    public enum ComparisonType
    {
        LessThan,
        LessThanEqual,
        Equal,
        GreaterThanEqual,
        GreaterThan,
    }

    public class Context
    {
        public Config Config { get; } = new Config();
        public string ConfigurationName { get; set; } = "SosConfig";
        public bool DryRun { get; set; }
        public bool ForceResync { get; set; }
        public bool NeverResync { get; set; }
        public DateTimeOffset? ChangesSince { get; set; }
        public int MaximumPointsPerObservation { get; set; } = 1000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan MaximumExportDuration { get; set; } = TimeSpan.FromHours(1);
    }
}
