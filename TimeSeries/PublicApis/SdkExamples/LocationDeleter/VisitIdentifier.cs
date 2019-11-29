using System;

namespace LocationDeleter
{
    public class VisitIdentifier
    {
        public DateTimeOffset StartDateTime { get; set; }
        public string Location { get; set; }

        public static bool TryParse(string text, out VisitIdentifier visitIdentifier)
        {
            visitIdentifier = VisitIdentifierParser.ParseIdentifier(text);

            return visitIdentifier != null;
        }

        public override string ToString()
        {
            return StartDateTime.TimeOfDay == TimeSpan.Zero
            ? $"{Location}@{StartDateTime:yyyy-MM-dd}"
            : $"{Location}@{StartDateTime:yyyy-MM-ddTHH:mm:ss}";
        }
    }
}
