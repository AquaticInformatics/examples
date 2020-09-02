

// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using Aquarius.Samples.Client.ServiceModel;

namespace LabFileImporter
{
    public class ImportResultResponse
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int ErrorCount { get; set; }
        public int NewCount { get; set; }
        public int UpdateCount { get; set; }
        public int ExpectedCount { get; set; }
        public string InvalidRowsCsvUrl { get; set; }
        public string SummaryReportText { get; set; }
        public List<ImportError> ImportJobErrors { get; set; }
        public List<ImportErrorItem> ErrorImportItems { get; set; }
    }

    public class ImportErrorItem
    {
        public Dictionary<string, List<ImportError>> Errors { get; set; }
        public string RowId { get; set; }
    }
}
