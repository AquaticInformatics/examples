using System;
using System.Linq;
using System.Reflection;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;
using Fastenshtein;

namespace PointZilla
{
    public class TimezoneHelper
    {
        // ReSharper disable once PossibleNullReferenceException
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static bool TryParseDateTimeZone(string text, out DateTimeZone dateTimeZone)
        {
            dateTimeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(text)
                           ?? DateTimeZoneProviders.Bcl.GetZoneOrNull(text);

            return dateTimeZone != null;
        }

        public static DateTimeZone ParseDateTimeZone(string text)
        {
            if (TryParseDateTimeZone(text, out var dateTimeZone))
                return dateTimeZone;

            throw new ExpectedException($"'{text}' is not a known timezone name. Use the /{nameof(Context.FindTimezones)}= option to find a matching name. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for details.");
        }

        private static readonly IDateTimeZoneProvider[] Providers =
        {
            DateTimeZoneProviders.Tzdb,
            DateTimeZoneProviders.Bcl,
        };

        public static void ShowMatchingTimezones(string text)
        {
            if (TryParseDateTimeZone(text, out var exactMatchZone))
                Log.Info($"'{text}' exactly matched {Summarize(exactMatchZone)}.");
            else
                Log.Info($"'{text}' did not exactly match any known timezone.");

            ShowCurrentTimezone();

            var anyEquivalentZones = ShowEquivalentZones(text);

            if (!anyEquivalentZones && exactMatchZone == null)
            {
                ShowSimilarlyNamedZones(text);
            }
        }

        private static void ShowCurrentTimezone()
        {
            var localZones = Providers
                .Select(p => p.GetSystemDefault())
                .Where(p => p != null)
                .ToList();

            Log.Info("");
            Log.Info("Current local timezone:");
            Log.Info("=======================");

            if (!localZones.Any())
            {
                Log.Info($"The current local timezone is unknown.");
                return;
            }

            foreach (var zone in localZones)
            {
                Log.Info(Summarize(zone));
            }
        }

        private static bool ShowEquivalentZones(string text)
        {
            var components = text
                .Split('/')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (components.Count != 2)
                return false;

            TryParseDateTimeZone(components[0], out var zone1);
            TryParseDateTimeZone(components[1], out var zone2);

            if (!IsSingleOffsetZone(zone1) || !IsSingleOffsetZone(zone2))
                return false;

            var minOffset = zone1.MinOffset < zone2.MinOffset ? zone1.MinOffset : zone2.MinOffset;
            var maxOffset = zone1.MaxOffset > zone2.MaxOffset ? zone1.MaxOffset : zone2.MaxOffset;

            var matchingZones = Providers
                .SelectMany(provider => provider.Ids.Select(id => provider[id]))
                .Where(zone =>
                {
                    var (min, max) = GetRecentOffsets(zone);

                    return min == minOffset && max == maxOffset;
                })
                .ToList();

            if (!matchingZones.Any())
                return false;

            Log.Info("");
            Log.Info("Equivalent timezones:");
            Log.Info("=====================");
            Log.Info($"'{text}' matches {"timezone".ToQuantity(matchingZones.Count)}:");

            foreach (var zone in matchingZones)
            {
                Log.Info(Summarize(zone));
            }

            return true;
        }

        private static bool IsSingleOffsetZone(DateTimeZone zone)
        {
            return zone != null && zone.MinOffset == zone.MaxOffset;
        }


        private static void ShowSimilarlyNamedZones(string text)
        {
            var allZones = Providers
                .SelectMany(provider => provider.Ids.Select(id => provider[id]))
                .ToList();

            var partialMatches = allZones
                .Where(zone => zone.Id.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0)
                .ToList();

            if (partialMatches.Any())
            {
                Log.Info("");
                Log.Info("Partially matched timezones:");
                Log.Info("============================");

                foreach (var zone in partialMatches)
                {
                    Log.Info(Summarize(zone));
                }

                return;
            }

            var levenshtein = new Levenshtein(text);

            var zonesByDistance = allZones
                .Select(zone => (Zone: zone, Distance: levenshtein.DistanceFrom(zone.Id)))
                .OrderBy(tuple => tuple.Distance)
                .ToList();

            const int maximumGuessDistance = 7;
            const int maximumGuesses = 4;

            var bestGuess = zonesByDistance.First();

            if (bestGuess.Distance > maximumGuessDistance)
                return;

            var guesses = zonesByDistance
                .Where(tuple => tuple.Distance <= bestGuess.Distance + 1)
                .OrderBy(tuple => tuple.Distance)
                .ThenByDescending(tuple => tuple.Zone.Id.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
                .ThenByDescending(tuple => tuple.Zone.Id.EndsWith(text, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (guesses.Count > maximumGuesses)
                guesses = zonesByDistance
                    .Where(tuple => tuple.Distance == bestGuess.Distance)
                    .OrderByDescending(tuple => tuple.Zone.Id.StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
                    .ThenByDescending(tuple => tuple.Zone.Id.EndsWith(text, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

            if (!guesses.Any() || guesses.Count > maximumGuesses)
                return;

            Log.Info("");
            Log.Info("Similarly named timezones:");
            Log.Info("==========================");

            foreach (var guess in guesses)
            {
                Log.Info($"Did you mean {Summarize(guess.Zone)}?");
            }
        }

        private static (Offset MinOffset, Offset MaxOffset) GetRecentOffsets(DateTimeZone zone)
        {
            var recentZoneIntervals = zone.GetZoneIntervals(
                    Instant.FromUtc(2010, 1, 1, 0, 0, 0),
                    SystemClock.Instance.Now)
                .ToList();

            if (!recentZoneIntervals.Any())
                return (Offset.MaxValue, Offset.MinValue);

            var minOffset = recentZoneIntervals.Select(zi => zi.WallOffset).Min();
            var maxOffset = recentZoneIntervals.Select(zi => zi.WallOffset).Max();

            return (minOffset, maxOffset);
        }

        private static string Summarize(DateTimeZone zone)
        {
            var (recentMinOffset, recentMaxOffset) = GetRecentOffsets(zone);

            return $"'{zone.Id}' Recent:[Min={recentMinOffset}, Max={recentMaxOffset}] Historical:[Min={zone.MinOffset}, Max={zone.MaxOffset}]";
        }
    }
}
