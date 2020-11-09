using System;

namespace NEM12PreProcessor
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
