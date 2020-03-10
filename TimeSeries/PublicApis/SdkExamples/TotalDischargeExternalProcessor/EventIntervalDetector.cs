using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;

namespace TotalDischargeExternalProcessor
{
    public class EventIntervalDetector
    {
        private List<Point> Points { get; }
        private Duration MinimumDuration { get; }

        public EventIntervalDetector(List<Point> points, TimeSpan minimumEventDuration)
        {
            if (points == null || !points.Any())
                throw new ArgumentException("No points in range", nameof(points));

            Points = points;
            MinimumDuration = Duration.FromTimeSpan(minimumEventDuration);
        }

        public IEnumerable<Interval> Detect()
        {
            var start = (Instant?) null;
            var prevTime = (Instant?) null;
            var prevValueStart = (Instant?)null;
            var prevValue = (double?) null;

            foreach (var point in Points)
            {
                var time = point.Time;
                var value = point.Value;

                if (value <= prevValue)
                {
                    // The signal is perfectly flat or falling
                    if (start.HasValue && MinimumDuration < time - prevValueStart.Value)
                    {
                        // This signal has remained flat for long enough to close the event after the minimum duration
                        yield return new Interval(start.Value, prevValueStart.Value.Plus(MinimumDuration));
                        start = null;
                    }
                }
                else if (value > prevValue)
                {
                    // The signal is rising. Extend the existing event
                    if (!start.HasValue)
                    {
                        // The previous point is the true start of the event
                        start = prevTime;
                    }
                }

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (prevValue != value)
                {
                    prevValueStart = time;
                }

                prevValue = value;
                prevTime = time;
            }

            if (start.HasValue)
            {
                // We have reached the end of the points, with an open event
                if (MinimumDuration < prevTime.Value - start.Value)
                {
                    // Just close the open event at the last known time
                    yield return new Interval(start.Value, prevTime.Value);
                }
                else
                {
                    // Close the open event at the minimum duration
                    yield return new Interval(start.Value, start.Value.Plus(MinimumDuration));
                }
            }
        }
    }
}
