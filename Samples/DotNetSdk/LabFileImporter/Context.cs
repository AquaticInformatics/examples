using System;
using System.Collections.Generic;
using Aquarius.Samples.Client.ServiceModel;
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
        public Dictionary<string, string> MethodAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, QualityControlType?> QCTypeAliases { get; } = new Dictionary<string, QualityControlType?>(StringComparer.InvariantCultureIgnoreCase);
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
        public bool VerboseErrors { get; set; }
        public int? MaximumObservations { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public bool? LaunchGui { get; set; }

        public class AliasedProperty
        {
            public string PropertyId { get; set; }
            public string UnitId { get; set; }
            public string AliasedPropertyId { get; set; }
            public string AliasedUnitId { get; set; }

            public string Key => $"{PropertyId}:{UnitId}";
        }
    }
}
