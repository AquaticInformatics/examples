using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ObservationValidator
{
    public static class RuleReader
    {
        private static readonly string RuleFileName = "ValidationRules.txt";

        public static List<Rule> ReadFromDefaultFile()
        {
            var rules = new List<Rule>();
            var fullPath = GetRuleFileFullPath();

            var allLines = File.ReadAllLines(fullPath);

            foreach (var line in allLines)
            {
                if (IsCommentLine(line) || string.IsNullOrWhiteSpace(line))
                    continue;

                var rule = ParseRuleOrThrow(line);

                rules.Add(rule);
            }

            return rules;
        }

        private static string GetRuleFileFullPath()
        {
            return FilePathHelper.GetFullPathOfFileInExecutableFolder(RuleFileName);
        }

        private static bool IsCommentLine(string line)
        {
            return line.Trim().StartsWith("#");
        }

        private static Rule ParseRuleOrThrow(string line)
        {
            var parts = Regex.Split(line, "([><=]{1,2})", RegexOptions.CultureInvariant);

            if (parts.Length != 3)
            {
                throw new ArgumentException($"Invalid rule '{line}'");
            }

            return new Rule(parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
        }
    }
}
