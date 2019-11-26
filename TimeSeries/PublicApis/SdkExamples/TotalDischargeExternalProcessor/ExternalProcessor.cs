using System.Linq;
using System.Reflection;
using ServiceStack.Logging;

namespace TotalDischargeExternalProcessor
{
    public class ExternalProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public void Run()
        {
            Validate();
        }

        private void Validate()
        {
            if (!Context.Processors.Any())
                throw new ExpectedException($"No processors configured. Nothing to do. Add a /{nameof(Context.Processors)}= option or positional argument.");
        }
    }
}
