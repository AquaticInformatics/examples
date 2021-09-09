using System;
using System.Collections.Generic;
using NodaTime;

namespace NWFWMDLabFileImporter
{
    public class Context
    {
        public string ServerUrl { get; set; }
        public string ApiToken { get; set; }
        public string CsvOutputPath { get; set; }
        public bool Overwrite { get; set; }
        public Offset UtcOffset { get; set; } = Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks);
        public Dictionary<string,string> LocationAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> ObservedPropertyAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> AnalysisMethodAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> UnitAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public List<string> Files { get; } = new List<string>();
        public bool DryRun { get; set; }
        public string ResultGrade { get; set; } = "OK";
        public string LabResultStatus { get; set; } = "Preliminary";
        public string DefaultLaboratory { get; set; } = "";
        public string DefaultMedium { get; set; } = "Water";
        public string NonDetectCondition { get; set; } = "Non-Detect";
        public bool StopOnFirstError { get; set; }
        public int ErrorLimit { get; set; } = 10;
        public bool VerboseErrors { get; set; }
        public int? MaximumObservations { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public bool? LaunchGui { get; set; }
    }
}
