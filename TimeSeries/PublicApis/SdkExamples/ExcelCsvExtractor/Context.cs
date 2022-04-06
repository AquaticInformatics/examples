using System.Collections.Generic;

namespace ExcelCsvExtractor
{
    public class Context 
    {
        public string ExcelPath { get; set; }
        public List<string> Sheets { get; } = new List<string>();
        public string OutputPath { get; set; }
        public bool Overwrite { get; set; }
        public string DateTimeFormat { get; set; }
        public bool TrimEmptyColumns { get; set; } = true;
    }
}
