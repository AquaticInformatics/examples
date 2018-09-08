using System;
using System.Collections.Generic;
using System.Linq;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;

namespace SosExporter
{
    public class ComputationPeriodEstimator
    {
        public const int MinimumPointCount = 100;

        public static ComputationPeriod InferPeriodFromRecentPoints(TimeSeriesDataServiceResponse timeSeries)
        {
            var recentPoints = timeSeries.Points
                .Skip(timeSeries.Points.Count - MinimumPointCount)
                .ToList();

            if (recentPoints.Count < 2)
                return ComputationPeriod.Unknown;

            var periodFrequencyBins = new Dictionary<ComputationPeriod, int>();

            for (var i = 0; i < recentPoints.Count - 1; ++i)
            {
                var timeSpan = recentPoints[i + 1].Timestamp.DateTimeOffset
                    .Subtract(recentPoints[i].Timestamp.DateTimeOffset);

                var period = FindClosestPeriod(timeSpan);

                if (!periodFrequencyBins.ContainsKey(period))
                {
                    periodFrequencyBins.Add(period, 0);
                }

                periodFrequencyBins[period] += 1;
            }

            var mostCommonPeriod = periodFrequencyBins
                .Select(kvp => kvp)
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;

            return mostCommonPeriod;
        }

        private static ComputationPeriod FindClosestPeriod(TimeSpan timeSpan)
        {
            if (timeSpan < CommonPeriods.First().TimeSpan)
                return ComputationPeriod.Unknown;

            if (timeSpan > CommonPeriods.Last().TimeSpan)
                return CommonPeriods.Last().Period;

            return CommonPeriods.First(p => timeSpan <= p.TimeSpan).Period;
        }

        private static readonly List<(ComputationPeriod Period, TimeSpan TimeSpan)> CommonPeriods =
            new List<(ComputationPeriod Period, TimeSpan TimeSpan)>
            {
                (ComputationPeriod.Minutes, TimeSpan.FromMinutes(1)),
                (ComputationPeriod.QuarterHourly, TimeSpan.FromMinutes(15)),
                (ComputationPeriod.Hourly, TimeSpan.FromHours(1)),
                (ComputationPeriod.Daily, TimeSpan.FromDays(1)),
                (ComputationPeriod.Weekly, TimeSpan.FromDays(7)),
                (ComputationPeriod.Monthly, TimeSpan.FromDays(28)),
                (ComputationPeriod.Annual, TimeSpan.FromDays(365)),
            };
    }
}
