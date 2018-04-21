using System.Text.RegularExpressions;

namespace TimeSeriesChangeMonitor
{
    public static class TimeSeriesIdentifierParser
    {
        public static string ParseLocationIdentifier(string timeSeriesIdentifier)
        {
            var match = IdentifierRegex.Match(timeSeriesIdentifier);

            if (!match.Success)
                throw new ExpectedException($"Can't parse '{timeSeriesIdentifier}' as time-series identifier. Expecting <Parameter>.<Label>@<Location>");

            return match.Groups["location"].Value;
        }

        private static readonly Regex IdentifierRegex = new Regex(@"^(?<identifier>(?<parameter>[^.]+)\.(?<label>[^@]+)@(?<location>.*))$");

        public static TimeSeriesIdentifier ParseIdentifier(string timeSeriesIdentifier)
        {
            var match = IdentifierRegex.Match(timeSeriesIdentifier);

            if (!match.Success)
                return null;

            return new TimeSeriesIdentifier
            {
                Parameter = match.Groups["parameter"].Value,
                Label = match.Groups["label"].Value,
                Location = match.Groups["location"].Value,
                Identifier = match.Groups["identifier"].Value,
            };
        }
    }
}
