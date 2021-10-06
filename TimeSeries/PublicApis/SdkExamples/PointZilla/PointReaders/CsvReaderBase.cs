using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.Text;
using ServiceStack.Logging;

namespace PointZilla.PointReaders
{
    public abstract class CsvReaderBase : PointReaderBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private InstantPattern TimePattern { get; }

        protected CsvReaderBase(Context context)
            : base(context)
        {
            TimePattern = string.IsNullOrWhiteSpace(Context.CsvDateTimeFormat)
                ? InstantPattern.ExtendedIsoPattern
                : InstantPattern.CreateWithInvariantCulture(Context.CsvDateTimeFormat);

            var isTimeFormatUtc = TimePattern.PatternText.Contains("'Z'");

            if (Context.CsvDateOnlyField != null)
            {
                isTimeFormatUtc = Context.CsvDateOnlyFormat.Contains("Z");
            }

            DefaultBias = isTimeFormatUtc
                ? Duration.Zero
                : Duration.FromTimeSpan((Context.UtcOffset ?? Offset.FromTicks(DateTimeOffset.Now.Offset.Ticks)).ToTimeSpan());
        }

        protected Instant? ParseInstant(string text)
        {
            var result = TimePattern.Parse(text);

            if (result.Success)
                return result.Value.Minus(DefaultBias);

            return null;
        }

        protected static void ParseField(string[] fields, int? fieldIndex, Action<string> parseAction)
        {
            if (!fieldIndex.HasValue)
                return;

            if (fieldIndex > 0 && fields.Length > fieldIndex - 1)
            {
                var text = fields[fieldIndex.Value - 1];

                if (!string.IsNullOrWhiteSpace(text))
                    parseAction(text);
            }
        }
    }
}
