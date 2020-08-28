using System;

namespace SamplesObservationExporter
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
