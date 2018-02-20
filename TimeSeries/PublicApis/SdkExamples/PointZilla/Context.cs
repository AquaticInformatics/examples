using System;
using System.Collections.Generic;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using NodaTime;

namespace PointZilla
{
    public class Context
    {
        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";

        public bool Wait { get; set; } = true;
        public TimeSpan? AppendTimeout { get; set; }

        public string TimeSeries { get; set; }
        public Interval? TimeRange { get; set; }
        public CommandType Command { get; set; } = CommandType.Auto;
        public int? GradeCode { get; set; }
        public List<string> Qualifiers { get; set; } = new List<string>();

        public TimeSeriesIdentifier SourceTimeSeries { get; set; }
        public Instant? SourceQueryFrom { get; set; }
        public Instant? SourceQueryTo { get; set; }

        public List<ReflectedTimeSeriesPoint> ManualPoints { get; set; } = new List<ReflectedTimeSeriesPoint>();

        public Instant StartTime { get; set; } = Instant.FromDateTimeUtc(DateTime.UtcNow);
        public TimeSpan PointInterval { get; set; } = TimeSpan.FromMinutes(1);
        public int NumberOfPoints { get; set; } // 0 means "derive the point count from number of periods"
        public double NumberOfPeriods { get; set; } = 1;
        public WaveformType WaveformType { get; set; } = WaveformType.SineWave;
        public double WaveformOffset { get; set; } = 0;
        public double WaveformPhase { get; set; } = 0;
        public double WaveformScalar { get; set; } = 1;
        public double WaveformPeriod { get; set; } = 1440;

        public List<string> CsvFiles { get; set; } = new List<string>();

        // Use defaults which match AQTS Export-from-Springboard CSV format
        public int CsvTimeField { get; set; } = 1;
        public int CsvValueField { get; set; } = 3;
        public int CsvGradeField { get; set; } = 5;
        public int CsvQualifiersField { get; set; } = 6;
        public string CsvComment { get; set; } = "#";
        public int CsvSkipRows { get; set; }
        public string CsvTimeFormat { get; set; }
        public bool CsvIgnoreInvalidRows { get; set; } = true;
        public bool CsvRealign { get; set; }
    }
}
