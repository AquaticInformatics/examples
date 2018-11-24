using System;
using System.Collections.Generic;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using NodaTime;

namespace PointZilla
{
    public class Context
    {
        public string ExecutingFileVersion { get; set; }

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

        public CreateMode CreateMode { get; set; } = CreateMode.Never;
        public Duration GapTolerance { get; set; } = DurationExtensions.MaxGapDuration;
        public Offset? UtcOffset { get; set; }
        public string Unit { get; set; }
        public InterpolationType? InterpolationType { get; set; }
        public bool Publish { get; set; }
        public string Description { get; set; } = "Created by PointZilla";
        public string Comment { get; set; }
        public string Method { get; set; }
        public string ComputationIdentifier { get; set; }
        public string ComputationPeriodIdentifier { get; set; }
        public string SubLocationIdentifier { get; set; }
        public List<ExtendedAttributeValue> ExtendedAttributeValues { get; set; } = new List<ExtendedAttributeValue>();
        public TimeSeriesType? TimeSeriesType { get; set; }

        public TimeSeriesIdentifier SourceTimeSeries { get; set; }
        public Instant? SourceQueryFrom { get; set; }
        public Instant? SourceQueryTo { get; set; }
        public string SaveCsvPath { get; set; }
        public bool StopAfterSavingCsv { get; set; }

        public List<ReflectedTimeSeriesPoint> ManualPoints { get; set; } = new List<ReflectedTimeSeriesPoint>();

        public Instant StartTime { get; set; } = Instant.FromDateTimeUtc(DateTime.UtcNow);
        public TimeSpan PointInterval { get; set; } = TimeSpan.FromMinutes(1);
        public int NumberOfPoints { get; set; } // 0 means "derive the point count from number of periods"
        public int BatchSize { get; set; } = 500_000;
        public double NumberOfPeriods { get; set; } = 1;
        public WaveformType WaveformType { get; set; } = WaveformType.SineWave;
        public double WaveformOffset { get; set; } = 0;
        public double WaveformPhase { get; set; } = 0;
        public double WaveformScalar { get; set; } = 1;
        public double WaveformPeriod { get; set; } = 1440;

        public List<string> CsvFiles { get; set; } = new List<string>();

        public int CsvTimeField { get; set; }
        public int CsvValueField { get; set; }
        public int CsvGradeField { get; set; }
        public int CsvQualifiersField { get; set; }
        public string CsvComment { get; set; }
        public int CsvSkipRows { get; set; }
        public string CsvTimeFormat { get; set; }
        public bool CsvIgnoreInvalidRows { get; set; }
        public bool CsvRealign { get; set; }
        public bool CsvRemoveDuplicatePoints { get; set; } = true;
    }
}
