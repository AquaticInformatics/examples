using System;
using System.Collections.Generic;
using System.Linq;

namespace PointZilla
{
    public static class QualifiersParser
    {
        public static List<string> Parse(string text)
        {
            var qualifiers = (text ?? string.Empty)
                .Split(QualifierDelimiters, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return qualifiers.Any()
                ? qualifiers
                : null;
        }

        private static readonly char[] QualifierDelimiters = { ',' };
    }
}
