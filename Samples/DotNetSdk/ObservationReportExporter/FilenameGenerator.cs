using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ObservationReportExporter
{
    public class FilenameGenerator
    {
        public const string TemplatePattern = "Template";
        public const string LocationPattern = "Location";
        public const string TimePattern = "Time";

        public const string DefaultAttachmentFilename = "{" + TemplatePattern + "}-{" + LocationPattern + "}.xlsx";

        public static string GenerateAttachmentFilename(string attachmentFilename, string exportTemplateName, string locationIdentifier, DateTimeOffset exportTime)
        {
            var patterns = new Dictionary<string, Func<string, string>>(StringComparer.InvariantCultureIgnoreCase)
            {
                { TemplatePattern, _ => exportTemplateName },
                { LocationPattern, _ => locationIdentifier },
                { TimePattern, format => exportTime.ToString(string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd" : format) }
            };

            return PatternRegex.Replace(attachmentFilename, match =>
            {
                var pattern = match.Groups["pattern"].Value.Trim();
                var format = match.Groups["format"].Value.Trim();

                if (!patterns.TryGetValue(pattern, out var replacement))
                    throw new ExpectedException($"'{pattern}' is not a known attachment filename substitution pattern. Try one of {{{string.Join("}, {", patterns.Keys)}}}.");

                return replacement(format);
            });
        }

        private static readonly Regex PatternRegex = new Regex(@"\{(?<pattern>[^:}]+)(:(?<format>[^}]+))?\}");
    }
}
