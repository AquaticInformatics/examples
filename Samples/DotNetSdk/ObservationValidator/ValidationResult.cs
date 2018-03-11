using System;
using System.Collections.Generic;

namespace ObservationValidator
{
    public class ValidationResult
    {
        public HashSet<string> ProcessedSpecimenNames => new HashSet<string>();
        public int ProcessedSpecimenCount { get; set; }
        public int ExaminedObservationsCount { get; set; }
        public int InvalidObservationsTotal { get; set; }
        public Exception FatalException { get; set; }
    }
}
