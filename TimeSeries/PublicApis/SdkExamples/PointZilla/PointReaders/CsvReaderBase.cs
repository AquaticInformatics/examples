using System;
using System.Linq;
using System.Reflection;
using NodaTime;
using NodaTime.Text;
using ServiceStack.Logging;

namespace PointZilla.PointReaders
{
    public abstract class CsvReaderBase : PointReaderBase
    {
        // ReSharper disable once PossibleNullReferenceException
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private InstantPattern InstantPattern { get; }
        private LocalDateTimePattern LocalDateTimePattern { get; }

        protected CsvReaderBase(Context context)
            : base(context)
        {
            InstantPattern = HasZoneInfo
                ? null
                : string.IsNullOrWhiteSpace(Context.CsvDateTimeFormat)
                    ? InstantPattern.ExtendedIso
                    : InstantPattern.CreateWithInvariantCulture(Context.CsvDateTimeFormat);

            LocalDateTimePattern = HasZoneInfo
                ? string.IsNullOrWhiteSpace(Context.CsvDateTimeFormat)
                    ? LocalDateTimePattern.ExtendedIso
                    : LocalDateTimePattern.CreateWithInvariantCulture(Context.CsvDateTimeFormat)
                : null;

            var isTimeFormatUtc = (InstantPattern?.PatternText.Contains("'Z'") ?? false) || (LocalDateTimePattern?.PatternText.Contains("'Z'") ?? false);

            if (Context.CsvDateOnlyField != null)
            {
                isTimeFormatUtc |= Context.CsvDateOnlyFormat.Contains("Z");
            }

            if (!isTimeFormatUtc)
                return;

            DefaultBias = Duration.Zero;

            if (!HasZoneInfo)
                return;

            var patterns = new[]
                {
                    InstantPattern?.PatternText,
                    LocalDateTimePattern?.PatternText,
                    Context.CsvDateOnlyFormat
                }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var settingMessage = Context.Timezone != null
                ? $"/{nameof(context.Timezone)}='{context.Timezone}'"
                : $"/{nameof(context.CsvTimezoneField)}={context.CsvTimezoneField}";

            Log.Warn($"Ignoring the {settingMessage} value since the time-format patterns \"{string.Join("\" and \"", patterns)}\" contain zone-info.");
            Context.Timezone = null;
            Context.CsvTimezoneField = null;
        }

        protected Instant? ParseInstant(string text, Func<DateTimeZone> zoneResolver = null)
        {
            if (LocalDateTimePattern != null)
            {
                var result = LocalDateTimePattern.Parse(text);

                if (result.Success)
                    return InstantFromLocalDateTime(result.Value, zoneResolver);
            }

            if (InstantPattern != null)
            {
                var result = InstantPattern.Parse(text);

                if (result.Success)
                    return result.Value.Minus(DefaultBias);
            }

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
