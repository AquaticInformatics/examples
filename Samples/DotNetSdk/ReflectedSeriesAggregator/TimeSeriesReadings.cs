using System;
using System.Collections.Generic;
using System.Linq;

namespace ReflectedSeriesAggregator
{
    public class TimeSeriesReadings
    {
        readonly SortedDictionary<DateTimeOffset, TimeSeriesReading> _tsData = new SortedDictionary<DateTimeOffset, TimeSeriesReading>();

        public int Count { get => _tsData.Count; }

        public bool Add(string seriesIdentifier, DateTimeOffset timestamp, double? value, bool alwaysInsert)
        {
            if (!_tsData.ContainsKey(timestamp))
            {
                _tsData.Add(timestamp, new TimeSeriesReading(seriesIdentifier, timestamp, value));
                return true;
            }

            if (alwaysInsert)
            {
                TimeSeriesReading timeSeriesReading = _tsData[timestamp];
                timeSeriesReading.SeriesIdentifier = seriesIdentifier;
                timeSeriesReading.Value = value;
                return true;
            }

            return false;
        }

        public bool Add(string seriesIdentifier, DateTimeOffset timestamp, double? value, int grade, bool alwaysInsert)
        {
            if (!_tsData.ContainsKey(timestamp))
            {
                _tsData.Add(timestamp, new TimeSeriesReading(seriesIdentifier, timestamp, value, grade));
                return true;
            }

            if (alwaysInsert)
            {
                TimeSeriesReading timeSeriesReading = _tsData[timestamp];
                timeSeriesReading.SeriesIdentifier = seriesIdentifier;
                timeSeriesReading.Value = value;
                timeSeriesReading.Grade = grade;
                return true;
            }

            return false;
        }


        public bool Add(TimeSeriesReading tsReading, bool alwaysInsert)
        {
            if (!_tsData.ContainsKey(tsReading.Timestamp))
            {
                _tsData.Add(tsReading.Timestamp, new TimeSeriesReading(tsReading.SeriesIdentifier, tsReading.Timestamp, tsReading.Value, tsReading.Grade));
                return true;
            }

            if (alwaysInsert)
            {
                TimeSeriesReading timeSeriesReading = _tsData[tsReading.Timestamp];
                timeSeriesReading.SeriesIdentifier = tsReading.SeriesIdentifier;
                timeSeriesReading.Value = tsReading.Value;
                timeSeriesReading.Grade = tsReading.Grade;
                return true;
            }

            return false;
        }

        public TimeSeriesReading GetReading(DateTimeOffset timestamp) => _tsData[timestamp];
        
        public void Clear() => _tsData.Clear();

        public bool IsEmpty { get => _tsData.Count == 0; }
       
        public List<TimeSeriesReading> ToList() => _tsData.Values.ToList();

        public DateTimeOffset? MinTimestamp => _tsData.Keys.FirstOrDefault();

        public DateTimeOffset? MaxTimestamp => _tsData.Keys.LastOrDefault();
    }
}
