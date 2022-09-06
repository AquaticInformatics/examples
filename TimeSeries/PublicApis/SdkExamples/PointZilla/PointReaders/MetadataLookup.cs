using System;
using System.Collections.Generic;
using System.Linq;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;

namespace PointZilla.PointReaders
{
    public class MetadataLookup<TMetadata> where TMetadata : TimeRange
    {
        private IEnumerator<TMetadata> Enumerator { get; }
        private TMetadata CurrentItem { get; set; }
        private List<TMetadata> CandidateItems { get; } = new List<TMetadata>();

        public MetadataLookup(IEnumerable<TMetadata> items)
        {
            Enumerator = items.GetEnumerator();

            AdvanceEnumerator();
        }

        private void AdvanceEnumerator()
        {
            CurrentItem = Enumerator.MoveNext()
                ? Enumerator.Current
                : null;
        }

        public TMetadata FirstOrDefault(DateTimeOffset timestamp)
        {
            do
            {
                if (IsItemValid(CurrentItem, timestamp))
                    return CurrentItem;

                if (IsItemExpired(CurrentItem, timestamp))
                {
                    AdvanceEnumerator();
                }
                else
                {
                    return null;
                }

            } while (true);
        }

        private static bool IsItemValid(TMetadata item, DateTimeOffset timestamp)
        {
            return item?.StartTime <= timestamp && timestamp < item.EndTime;
        }

        private static bool IsItemExpired(TMetadata item, DateTimeOffset timestamp)
        {
            return item?.EndTime <= timestamp;
        }

        public IEnumerable<TMetadata> GetMany(DateTimeOffset timestamp)
        {
            if (IsItemValid(CurrentItem, timestamp))
            {
                while (IsItemValid(CurrentItem, timestamp))
                {
                    CandidateItems.Add(CurrentItem);

                    AdvanceEnumerator();
                }
            }

            var expiredItems = CandidateItems
                .Where(item => IsItemExpired(item, timestamp))
                .ToList();

            if (expiredItems.Any())
            {
                CandidateItems.RemoveAll(item => IsItemExpired(item, timestamp));
            }

            return CandidateItems
                .Where(item => IsItemValid(item, timestamp));
        }
    }

    public static class MetadataExtensions
    {
        // Use for: Grade, Approval, Method, GapTolerance, InterpolationType
        // Each of these metadata types can have exactly one or no applicable metadata at any given time.
        // InterpolationType is fixed for the lifetime of the series. Only one InterpolationType will exist.
        public static T ResolveSingleMetadata<TMetadata, T>(this MetadataLookup<TMetadata> lookup, DateTimeOffset time, Func<TMetadata, T> func)
            where TMetadata : TimeRange
        {
            var metadata = lookup.FirstOrDefault(time);

            return metadata == null ? default : func(metadata);
        }

        // Use for: Qualifier, Note, CorrectionOperation
        // Each of these metadata types can have zero or more applicable items at any given time
        public static IEnumerable<T> ResolveOverlappingMetadata<TMetadata, T>(this MetadataLookup<TMetadata> lookup, DateTimeOffset time, Func<TMetadata, T> func)
            where TMetadata : TimeRange
        {
            return lookup
                .GetMany(time)
                .Select(func);
        }
    }
}
