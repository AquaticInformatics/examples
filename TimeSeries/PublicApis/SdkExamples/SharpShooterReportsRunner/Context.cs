using System;
using System.Collections.Generic;

namespace SharpShooterReportsRunner
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public string TemplatePath { get; set; }
        public string OutputPath { get; set; }
        public bool LaunchReportDesigner { get; set; }
        public string UploadedReportLocationIdentifier { get; set; }
        public string UploadedReportTitle { get; set; }
        public List<TimeSeries> TimeSeries { get; set; } = new List<TimeSeries>();
        public List<string> FieldVisits { get; set; } = new List<string>();
        public List<ExternalDataSet> ExternalDataSets { get; set; } = new List<ExternalDataSet>();
        public Dictionary<string,string> ReportParameters { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> ParameterOverrides { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    }
}
