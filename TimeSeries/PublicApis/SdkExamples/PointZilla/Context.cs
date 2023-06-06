using System;
using System.Collections.Generic;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using NodaTime;
using PointZilla.DbClient;

namespace PointZilla
{
    public class Context
    {
        public string ExecutingFileVersion { get; set; }

        public string Server { get; set; }
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
        public string SessionToken { get; set; }

        public bool Wait { get; set; } = true;
        public TimeSpan? AppendTimeout { get; set; }

        public string TimeSeries { get; set; }
        public Interval? TimeRange { get; set; }
        public CommandType Command { get; set; } = CommandType.Auto;
        public int? GradeCode { get; set; }
        public List<string> Qualifiers { get; set; }

        public bool IgnoreGrades { get; set; }
        public bool GradeMappingEnabled { get; set; }
        public int? MappedDefaultGrade { get; set; }
        public Dictionary<int, int?> MappedGrades { get; } = new Dictionary<int, int?>();
        public bool IgnoreQualifiers { get; set; }
        public bool QualifierMappingEnabled { get; set; }
        public List<string> MappedDefaultQualifiers { get; set; }
        public Dictionary<string,string> MappedQualifiers { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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

        public DateTimeZone Timezone { get; set; }
        public string FindTimezones { get; set; }
        public Dictionary<string, string> TimezoneAliases { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public TimeSeriesIdentifier SourceTimeSeries { get; set; }
        public Instant? SourceQueryFrom { get; set; }
        public Instant? SourceQueryTo { get; set; }
        public string SaveCsvPath { get; set; }
        public bool StopAfterSavingCsv { get; set; }
        public SaveNotesMode SaveNotesMode { get; set; }

        public List<TimeSeriesPoint> ManualPoints { get; set; } = new List<TimeSeriesPoint>();
        public List<TimeSeriesNote> ManualNotes { get; set; } = new List<TimeSeriesNote>();

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
        public string WaveFormTextX { get; set; }
        public string WaveFormTextY { get; set; }

        public List<string> CsvFiles { get; set; } = new List<string>();
        public string CsvNotesFile { get; set; }

        public Field CsvDateTimeField { get; set; }
        public string CsvDateTimeFormat { get; set; }
        public Field CsvDateOnlyField { get; set; }
        public string CsvDateOnlyFormat { get; set; }
        public Field CsvTimeOnlyField { get; set; }
        public string CsvTimeOnlyFormat { get; set; }
        public string CsvDefaultTimeOfDay { get; set; } = "00:00";
        public Field CsvValueField { get; set; }
        public Field CsvGradeField { get; set; }
        public Field CsvQualifiersField { get; set; }
        public Field CsvNotesField { get; set; }
        public Field CsvTimezoneField { get; set; }
        public string CsvComment { get; set; }
        public int CsvSkipRows { get; set; }
        public int CsvSkipRowsAfterHeader { get; set; }
        public bool CsvHasHeaderRow { get; set; }
        public bool CsvIgnoreInvalidRows { get; set; }
        public string CsvHeaderStartsWith { get; set; }
        public bool CsvRealign { get; set; }
        public bool CsvRemoveDuplicatePoints { get; set; } = true;
        public bool CsvWarnDuplicatePoints { get; set; } = true;
        public string CsvDelimiter { get; set; } = ",";
        public string CsvNanValue { get; set; }
        public int? ExcelSheetNumber { get; set; }
        public string ExcelSheetName { get; set; }

        public DbType? DbType { get; set; }
        public string DbConnectionString { get; set; }
        public string DbQuery { get; set; }
        public string DbNotesQuery { get; set; }

        public bool IgnoreNotes { get; set; }
        public Field NoteStartField { get; set; } = Field.Parse("StartTime", nameof(Context.NoteStartField));
        public Field NoteEndField { get; set; } = Field.Parse("EndTime", nameof(Context.NoteEndField));
        public Field NoteTextField { get; set; } = Field.Parse("NoteText", nameof(Context.NoteTextField));

        public bool MultiRunStdin { get; set; }
    }
}
