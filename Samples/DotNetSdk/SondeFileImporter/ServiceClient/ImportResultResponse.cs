

// ReSharper disable InconsistentNaming

namespace SondeFileImporter.ServiceClient
{
    public class ImportResultResponse
    {
        public int successCount { get; set; }
        public int skippedCount { get; set; }
        public int errorCount { get; set; }
        public int newCount { get; set; }
        public int updateCount { get; set; }
        public int expectedCount { get; set; }
        public string invalidRowsCsvUrl { get; set; }
    }
}
