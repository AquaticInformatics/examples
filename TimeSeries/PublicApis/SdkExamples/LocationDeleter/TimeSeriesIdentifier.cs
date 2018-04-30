namespace LocationDeleter
{
    public class TimeSeriesIdentifier
    {
        public string Parameter { get; set; }
        public string Label { get; set; }
        public string Location { get; set; }
        public string Identifier { get; set; }

        public static bool TryParse(string text, out TimeSeriesIdentifier timeSeriesIdentifier)
        {
            timeSeriesIdentifier = TimeSeriesIdentifierParser.ParseIdentifier(text);

            return timeSeriesIdentifier != null;
        }

        public override string ToString()
        {
            return Identifier;
        }
    }
}
