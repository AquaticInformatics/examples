using System;

namespace TimeSeriesChangeMonitor
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
