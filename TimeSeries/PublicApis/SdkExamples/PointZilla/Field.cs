using System;
using System.Text.RegularExpressions;

namespace PointZilla
{
    public class Field
    {
        public static Field Parse(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ExpectedException($"{fieldName}: You must specify a positive integer or a named column for a field");

            if (int.TryParse(text, out var index))
            {
                if (index > 0)
                    return new Field(fieldName, index);

                throw new Exception($"{fieldName}: {text} is an invalid index. Use a positive integer or a named column.");
            }

            var match = FieldRegex.Match(text);

            if (match.Success)
            {
                var patternText = match.Groups["pattern"].Value;
                var countText = match.Groups["count"].Value;
                
                Regex regex;

                try
                {
                    regex = new Regex(patternText);
                }
                catch (ArgumentException exception)
                {
                    throw new ExpectedException($"{fieldName}: {text} is not a valid regular expression: {exception.Message}");
                }

                if (!int.TryParse(countText, out var count))
                    count = 1;

                return new Field(fieldName, regex, count);
            }

            return new Field(fieldName, text);
        }

        public int ColumnIndex { get; set; }
        public string ColumnName { get; }
        public string FieldName { get; }
        public int PatternCount { get; }
        public Regex ColumnRegex { get; }

        public bool HasColumnName => !string.IsNullOrWhiteSpace(ColumnName);
        public bool HasColumnRegex => ColumnRegex != null;
        public bool HasColumnIndex => ColumnIndex > 0;

        private Field(string fieldName, int columnIndex)
        {
            FieldName = fieldName;
            ColumnIndex = columnIndex;
        }

        private Field(string fieldName, string columnName)
        {
            FieldName = fieldName;
            ColumnName = columnName;
        }

        private Field(string fieldName, Regex columnRegex, int patternCount)
        {
            FieldName = fieldName;
            ColumnRegex = columnRegex;
            PatternCount = patternCount;
        }

        public override string ToString()
        {
            return HasColumnName
                ? $"{FieldName}:'{ColumnName}'"
                : HasColumnRegex
                    ? PatternCount > 1
                        ? $"{FieldName}:/{ColumnRegex}/#{PatternCount}"
                        : $"{FieldName}:/{ColumnRegex}/"
                    : $"{FieldName}:#{ColumnIndex}";
        }

        private static readonly Regex FieldRegex = new Regex(@"^/(?<pattern>.+)/(#(?<count>\d+))?$");
    }
}
