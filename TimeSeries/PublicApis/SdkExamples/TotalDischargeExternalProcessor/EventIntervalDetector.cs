using System;
using System.Collections.Generic;
using System.Linq;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using NodaTime;

namespace TotalDischargeExternalProcessor
{
    public class EventIntervalDetector
    {
        private List<TimeSeriesPoint> Points { get; }
        private Duration MinimumDuration { get; }

        public EventIntervalDetector(List<TimeSeriesPoint> points, TimeSpan minimumEventDuration)
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
                if (!point.Value.HasValue || !point.Time.HasValue)
                    continue; // Just skip over any event gaps

                var time = point.Time;
                var value = point.Value.Value;

                if (value < prevValue)
                {
                    // The signal is falling. This should be the start of an event
                    if (start.HasValue && MinimumDuration < prevTime.Value - start.Value)
                    {
                        // Flush any previous interval before starting a new one
                        yield return new Interval(start.Value, prevTime.Value);
                    }

                    // Start tracking the next event
                    start = time;
                }
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                else if (value == prevValue)
                {
                    // The signal is perfectly flat
                    if (start.HasValue && MinimumDuration < time - prevValueStart.Value)
                    {
                        // This signal has remained flat for long enough to close the event after the minimum duration
                        yield return new Interval(start.Value, prevValueStart.Value.Plus(MinimumDuration));
                        start = null;
                    }
                }
                else if (value > prevValue)
                {
                    // The signal is rising. The existing event
                    if (!start.HasValue)
                    {
                        start = prevValueStart;
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
