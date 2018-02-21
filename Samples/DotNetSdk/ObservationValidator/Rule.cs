using System;
using System.Linq;

namespace ObservationValidator
{
    public class Rule
    {
        public string LeftParam { get; }
        public string ComparisonSymbol { get; }
        public string RightParam { get; }

        public Rule(string left, string comparisonSymbol, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                throw new ArgumentNullException(nameof(left),
                    "Parameter name on the left side of a rule cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(right))
            {
                throw new ArgumentNullException(nameof(left),
                    "Parameter name on the right side of a rule cannot be empty.");
            }

            ValidateComparisonSymbolOrThrow(comparisonSymbol);

            LeftParam = left;
            RightParam = right;
            ComparisonSymbol = comparisonSymbol;
        }

        private void ValidateComparisonSymbolOrThrow(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol) ||
                !ObservationValidator.ComparisonSymbol.AllSymbols.Contains(symbol))
            {
                throw new ArgumentException($"Invalid comparison symbol: {symbol}.");
            }
        }
    }
}
