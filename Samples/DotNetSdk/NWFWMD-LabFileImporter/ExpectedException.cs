using System;

namespace NWFWMDLabFileImporter
{
    public class ExpectedException : Exception
    {
        public ExpectedException(string message)
            : base(message)
        {
        }
    }
}
