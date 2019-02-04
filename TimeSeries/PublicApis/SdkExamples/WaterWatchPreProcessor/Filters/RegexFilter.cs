using System.Text.RegularExpressions;

namespace WaterWatchPreProcessor.Filters
{
    public class RegexFilter : IFilter
    {
        public bool Exclude { get; set; }
        public Regex Regex { get; set; }
    }
}
