using System.Text.RegularExpressions;

namespace LocationDeleter
{
    public static class RatingModelIdentifierParser
    {
        public static string ParseLocationIdentifier(string ratingModelIdentifier)
        {
            var match = IdentifierRegex.Match(ratingModelIdentifier);

            if (!match.Success)
                throw new ExpectedException($"Can't parse '{ratingModelIdentifier}' as rating model identifier. Expecting <InputParameter>-<OutputParameter>.<Label>@<Location>");

            return match.Groups["location"].Value;
        }

        private static readonly Regex IdentifierRegex = new Regex(@"^(?<identifier>(?<inputParameter>[^-]+)-(?<outputParameter>[^.]+)\.(?<label>[^@]+)@(?<location>.*))$");

        public static RatingModelIdentifier ParseIdentifier(string ratingModelIdentifier)
        {
            var match = IdentifierRegex.Match(ratingModelIdentifier);

            if (!match.Success)
                return null;

            return new RatingModelIdentifier
            {
                InputParameter = match.Groups["inputParameter"].Value,
                OutputParameter = match.Groups["outputParameter"].Value,
                Label = match.Groups["label"].Value,
                Location = match.Groups["location"].Value,
                Identifier = match.Groups["identifier"].Value,
            };
        }
    }
}
