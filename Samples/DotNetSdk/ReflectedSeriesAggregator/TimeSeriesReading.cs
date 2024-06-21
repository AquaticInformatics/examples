using System;

namespace ReflectedSeriesAggregator
{
    [Serializable]
    public class TimeSeriesReading
    {
        public string SeriesIdentifier { get; set; }
        public DateTimeOffset Timestamp { get; }
        public double? Value { get; set; }

        public int? Grade { get; set; }
       
        public TimeSeriesReading(string seriesIdentifier, DateTimeOffset datetime, double? val, int? grade = null)
        {
            SeriesIdentifier = seriesIdentifier;
            Timestamp = datetime;
            Value = val;
            Grade = grade;            
        }

        public override string ToString()
        {
            if( Grade == null)
                return $"{SeriesIdentifier},{ToIsoString(Timestamp)},{Value ?? double.NaN}";
            
            return $"{SeriesIdentifier},{ToIsoString(Timestamp)},{Value ?? double.NaN},{Grade}";
        }

        static string ToIsoString(DateTimeOffset datetime) => datetime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

