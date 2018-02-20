using System;
using System.Collections.Generic;
using System.Linq;

namespace PointZilla
{
    public static class QualifiersParser
    {
        public static List<string> Parse(string text)
        {
            return (text ?? string.Empty)
                .Split(QualifiersDelimiters, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private static readonly char[] QualifiersDelimiters = {','};
    }
}
