using System;

namespace TotalDischargeExternalProcessor
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
