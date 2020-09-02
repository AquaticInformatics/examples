using System;
using System.Collections.Generic;
using NodaTime;

namespace LabFileImporter
{
    public class Context
    {
        public string ServerUrl { get; set; }
        public string ApiToken { get; set; }
        public string CsvOutputPath { get; set; }
        public bool Overwrite { get; set; }
        public Offset UtcOffset { get; set; } = Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks);
        public Dictionary<string,string> LocationAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, AliasedProperty> ObservedPropertyAliases { get; } = new Dictionary<string, AliasedProperty>(StringComparer.InvariantCultureIgnoreCase);
        public List<string> Files { get; } = new List<string>();
        public bool DryRun { get; set; }
        public string ResultGrade { get; set; }
        public string LabResultStatus { get; set; } = "Requested";
        public string FieldResultStatus { get; set; } = "Preliminary";
        public string EstimatedGrade { get; set; } = "Estimated";
        public string DefaultLaboratory { get; set; } = "Unity Water";
        public string DefaultMedium { get; set; } = "Environmental Water";
        public string NonDetectCondition { get; set; } = "Non-Detect";
        public string LabSpecimenName { get; set; } = "Properties";
        public string BulkImportIndicator { get; set; } = "unity water internal";
        public string FieldResultPrefix { get; set; } = "client";
        public bool StopOnFirstError { get; set; }
        public int ErrorLimit { get; set; } = 10;
        public int? MaximumObservations { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }


        public class AliasedProperty
        {
            public string PropertyId { get; set; }
            public string UnitId { get; set; }

            public string Key => $"{PropertyId}:{UnitId}";
        }
    }
}
