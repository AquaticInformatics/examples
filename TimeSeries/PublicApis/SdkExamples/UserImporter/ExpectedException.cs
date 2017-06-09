using System;

namespace UserImporter
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
            
        }
    }
}
