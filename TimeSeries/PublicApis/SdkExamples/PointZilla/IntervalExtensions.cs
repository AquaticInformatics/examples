using NodaTime;

namespace PointZilla
{
    public static class IntervalExtensions
    {
        public static bool Intersects(this Interval interval1, Interval interval2)
        {
            return !interval1.Disjoint(interval2);
        }

        public static bool Intersects(this Interval? interval1, Interval? interval2)
        {
            return interval1.HasValue && interval2.HasValue && interval1.Value.Intersects(interval2.Value);
        }

        private static bool Disjoint(this Interval interval1, Interval interval2)
        {
            return interval1.IsEmpty() ||
                   interval2.IsEmpty() ||
                   interval1.End <= interval2.Start ||
                   interval2.End <= interval1.Start;
        }

        public static bool IsEmpty(this Interval interval)
        {
            return interval.Start.Equals(interval.End);
        }
    }
}
