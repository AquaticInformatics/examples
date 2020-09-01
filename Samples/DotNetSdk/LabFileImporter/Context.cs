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
        public Dictionary<string,string> ObservedPropertyAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public List<string> Files { get; } = new List<string>();
    }
}
