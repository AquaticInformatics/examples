using System.Collections.Generic;

namespace ObservationValidator
{
    public static class ComparisonSymbol
    {
        public const string GreaterThan = ">";
        public const string LessThan = "<";
        public const string GreaterOrEqual = ">=";
        public const string LessOrEqual = "<=";
        public const string Equal = "=";

        public static IReadOnlyList<string> AllSymbols =
            new List<string>
            {
                GreaterThan,
                LessThan,
                GreaterOrEqual,
                LessOrEqual,
                Equal
            };
    }
}
