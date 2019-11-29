using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LocationDeleter
{
    public class VisitIdentifierParser
    {
        public static string ParseLocationIdentifier(string visitIdentifier)
        {
            var match = IdentifierRegex.Match(visitIdentifier);

            if (!match.Success)
                throw new ExpectedException($"Can't parse '{visitIdentifier}' as visit identifier. Expecting location@date or location@dateTime");

            return match.Groups["location"].Value;
        }

        private static readonly Regex IdentifierRegex = new Regex(@"^(?<location>[^@]+)@(?<date>.*)$");

        public static VisitIdentifier ParseIdentifier(string visitIdentifier)
        {
            var match = IdentifierRegex.Match(visitIdentifier);

            if (!match.Success)
                return null;

            var dateText = match.Groups["date"].Value;

            if (DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault,
                    out var dateTime) && dateTime.Date == DateTime.MinValue)
                throw new ExpectedException($"'{visitIdentifier}' does not include a date component");

            if (!DateTimeOffset.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces
                                                                                 | DateTimeStyles.AssumeLocal,
                out var dateTimeOffset))
                return null;

            return new VisitIdentifier
            {
                Location = match.Groups["location"].Value,
                StartDateTime = dateTimeOffset
            };
        }
    }
}
