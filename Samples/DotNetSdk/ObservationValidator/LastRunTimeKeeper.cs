using System;
using System.Globalization;
using System.IO;

namespace ObservationValidator
{
    public static class LastRunTimeKeeper
    {
        private static readonly string DateTimeOffsetFormat = "O"; //Corresponds to "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffzzz"

        private static readonly string LastRunStartTimeFileName = "LastRunStartTime.txt";

        public static DateTimeOffset GetLastRunStartTimeUtc()
        {
            var fullPath = FilePathHelper.GetFullPathOfFileInExecutableFolder(LastRunStartTimeFileName);

            if (!File.Exists(fullPath))
                return DateTimeOffset.MinValue.ToUniversalTime();

            return GetDateTimeOffsetFromFile(fullPath);
        }

        private static DateTimeOffset GetDateTimeOffsetFromFile(string fullPath)
        {
            var lines = File.ReadAllLines(fullPath);

            foreach (var line in lines)
            {
                if(string.IsNullOrWhiteSpace(line))
                    continue;

                return DateTimeOffset.ParseExact(line.Trim(), DateTimeOffsetFormat, CultureInfo.InvariantCulture);
            }

            return DateTimeOffset.MinValue.ToUniversalTime();
        }

        public static void WriteDateTimeOffsetToFile(DateTimeOffset dateTimeOffset)
        {
            var fullPath = FilePathHelper.GetFullPathOfFileInExecutableFolder(LastRunStartTimeFileName);
            var timeString = dateTimeOffset.ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture);

            File.WriteAllText(fullPath, timeString);
        }
    }
}
