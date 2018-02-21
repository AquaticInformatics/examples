using System;

namespace ObservationValidator
{
    public class Context
    {
        public static readonly string PredefinedFlag = "InvalidValue";
        public static readonly int DefaultBatchSize = 100;

        public Context()
        {
            Flag = PredefinedFlag;
            BatchSize = DefaultBatchSize;
        }

        public string SamplesAuthToken { get; set; }
        public string SamplesApiBaseUrl { get; set; }
        public string Flag { get; set; }
        public int BatchSize { get; set; }
        public DateTimeOffset LastRunStartTimeUtc { get; set; }
    }
}
