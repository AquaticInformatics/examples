using System;
using System.Collections.Generic;

namespace UserImporter.Helpers
{
    public static class CommandLineArgumentParser
    {
        public static KeyValuePair<string, string> Parse(string argument)
        {
            var startsWithSlash = argument.StartsWith("/");
            var indexOfArgumentSeparator = argument.IndexOf("=", StringComparison.Ordinal);
            KeyValuePair<string, string> argumentPair;

            if (startsWithSlash && indexOfArgumentSeparator > 0)
            {
                var key = argument.Substring(1, indexOfArgumentSeparator - 1);
                var value = argument.Substring(indexOfArgumentSeparator + 1);
                argumentPair = new KeyValuePair<string, string>(key, value);
            }
            else
            {
                var errorMessage = $"Invalid command line argument: {argument}";
                throw new ArgumentException(errorMessage);
            }

            return argumentPair;
        }
    }
}
