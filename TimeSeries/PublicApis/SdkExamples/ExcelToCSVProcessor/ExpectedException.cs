using System;

namespace ExcelToCSVProcessor
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
