using System;
using System.Collections.Generic;

namespace ObservationReportExporter
{
    public class Context
    {
        public string SamplesServer { get; set; }
        public string SamplesApiToken { get; set; }
        public string TimeSeriesServer { get; set; }
        public string TimeSeriesUsername { get; set; }
        public string TimeSeriesPassword { get; set; }
        public string ExportTemplateName { get; set; }
        public string AttachmentFilename { get; set; } = FilenameGenerator.DefaultAttachmentFilename;
        public bool DryRun { get; set; }
        public bool DeleteExistingAttachments { get; set; } = true;

        public Dictionary<string, string> AttachmentTags { get; } =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public List<string> LocationIds { get; } = new List<string>();
        public List<string> LocationGroupIds { get; } = new List<string>();
        public List<string> AnalyticalGroupIds { get; } = new List<string>();
        public List<string> ObservedPropertyIds { get; } = new List<string>();
        public DateTimeOffset? ExportTime { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
    }
}
