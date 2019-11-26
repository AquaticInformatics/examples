using System.Reflection;
using ServiceStack.Logging;

namespace TotalDischargeExternalProcessor
{
    public class Processor
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public void Run()
        {

        }
    }
}
