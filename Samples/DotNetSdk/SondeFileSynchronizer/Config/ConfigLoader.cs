using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SondeFileSynchronizer.Config
{
    public class ConfigLoader
    {
        private static readonly string ConfigIniFilePath =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Config.ini";

        private static readonly string NameValueSeparator = "=";
        private static readonly string CommentLineStart = "#";

        public static Context FromConfigFile()
        {
            EnsureConfigFileExists();
            var lines = File.ReadAllLines(ConfigIniFilePath);

            return ParseLinesForContext(lines);
        }

        private static void EnsureConfigFileExists()
        {
            if (!File.Exists(ConfigIniFilePath))
            {
                throw new ArgumentException("Missing Config.ini file in the tool's folder.");
            }
        }

        private static Context ParseLinesForContext(string[] lines)
        {
            var context = new Context();
            var settingNameValues = new Dictionary<string,string>();

            var index = 0;
            while (index < lines.Length)
            {
                var line = lines[index++];

                if (IsLineToIgnore(line))
                {
                    continue;
                }

                if (IsSectionStart(line))
                {
                    index = ParseSection(lines, --index, context);
                    continue;
                }

                ParseForSetting(line, settingNameValues);
            }

            context.Setting = new Setting(settingNameValues);
            return context;
        }

        private static bool IsLineToIgnore(string line)
        {
            return string.IsNullOrWhiteSpace(line) ||
                   line.Trim().StartsWith(CommentLineStart);
        }

        private static bool IsSectionStart(string line)
        {
            var trimmedLine = line?.Trim();

            return !string.IsNullOrWhiteSpace(trimmedLine) &&
                   trimmedLine.EndsWith(ConfigNames.MappingSection, StringComparison.InvariantCultureIgnoreCase);
        }

        private static int ParseSection(string[] lines, int currentIndex, Context context)
        {
            var sectionName = lines[currentIndex].Trim();

            var sectionLines = new List<string>();
            var index = ++currentIndex;
            while (index < lines.Length)
            {
                var line = lines[index++];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsSectionStart(line))
                {
                    index--;
                    break;
                }

                sectionLines.Add(line);
            }

            var mapping = ParseLinesToDictionary(sectionLines);

            SetSectionMappingToContext(sectionName, mapping, context);

            return index;
        }

        private static void SetSectionMappingToContext(string sectionName,
            Dictionary<string, string> mappings, Context context)
        {
            switch (sectionName)
            {
                case ConfigNames.HeaderMappingSection:
                    context.HeaderMap = mappings;
                    return;
                case ConfigNames.HeaderPropertyIdMappingSection:
                    context.HeaderPropertyIdMap = mappings;
                    return;
                case ConfigNames.PropertyIdUnitMappingSection:
                    context.PropertyIdUnitMap = mappings;
                    return;
            }

            throw new ArgumentException($"Invalid section name '{sectionName}' in the config file.");
        }

        private static Dictionary<string, string> ParseLinesToDictionary(List<string> sectionLines)
        {
            return sectionLines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(SplitToNameValuePair)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static void ParseForSetting(string line, Dictionary<string,string> dic)
        {
            var keyValuePair = SplitToNameValuePair(line);
            if (string.IsNullOrWhiteSpace(keyValuePair.Key))
            {
                return;
            }

            dic[keyValuePair.Key] = keyValuePair.Value;
        }

        private static KeyValuePair<string,string> SplitToNameValuePair(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return new KeyValuePair<string, string>();
            }

            var parts = line.Split(NameValueSeparator.ToCharArray())
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (parts.Count != 2)
            {
                throw new ArgumentException($"Invalid config line:'{line}'");
            }

            return new KeyValuePair<string, string>(parts.First(), parts.Last());
        }
    }
}
