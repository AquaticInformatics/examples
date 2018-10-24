namespace LocationDeleter
{
    public class RatingModelIdentifier
    {
        public string InputParameter { get; set; }
        public string OutputParameter { get; set; }
        public string Label { get; set; }
        public string Location { get; set; }
        public string Identifier { get; set; }

        public static bool TryParse(string text, out RatingModelIdentifier ratingModelIdentifier)
        {
            ratingModelIdentifier = RatingModelIdentifierParser.ParseIdentifier(text);

            return ratingModelIdentifier != null;
        }

        public override string ToString()
        {
            return Identifier;
        }
    }
}
