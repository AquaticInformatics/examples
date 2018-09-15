using System.Text;

namespace PointZilla
{
    public class TimeSeriesIdentifier
    {
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Identifier { get; set; }
        public string Parameter { get; set; }
        public string Label { get; set; }
        public string LocationIdentifier { get; set; }

        public static bool TryParse(string text, out TimeSeriesIdentifier timeSeriesIdentifier)
        {
            timeSeriesIdentifier = TimeSeriesIdentifierParser.ParseExtendedIdentifier(text);

            return timeSeriesIdentifier != null;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(Server))
            {
                builder.Append("[");
                builder.Append(Server);

                if (!string.IsNullOrEmpty(Username))
                {
                    builder.Append($":{Username}:{Password}");
                }

                builder.Append("]");
            }

            builder.Append(Identifier);

            return builder.ToString();
        }
    }
}
