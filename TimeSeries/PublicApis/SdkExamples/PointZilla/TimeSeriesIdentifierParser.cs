using System.Text.RegularExpressions;

namespace PointZilla
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

        private static readonly Regex IdentifierRegex = new Regex(@"^(\[(?<server>(https?://)?[^:\]]+)(:(?<username>[^:]+):(?<password>[^\]]+))?\])?(?<identifier>(?<parameter>[^.]+)\.(?<label>[^@]+)@(?<location>.*))$");

        public static TimeSeriesIdentifier ParseExtendedIdentifier(string extendedIdentifier)
        {
            var match = IdentifierRegex.Match(extendedIdentifier);

            if (!match.Success)
                return null;

            return new TimeSeriesIdentifier
            {
                Server = match.Groups["server"].Value,
                Username = match.Groups["username"].Value,
                Password = match.Groups["password"].Value,
                Identifier = match.Groups["identifier"].Value,
                Parameter = match.Groups["parameter"].Value,
                Label = match.Groups["label"].Value,
                LocationIdentifier = match.Groups["location"].Value
            };
        }
    }
}
