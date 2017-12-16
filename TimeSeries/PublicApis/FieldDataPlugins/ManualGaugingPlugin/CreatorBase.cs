using FieldDataPluginFramework;
using ManualGaugingPlugin.FileData;

namespace ManualGaugingPlugin
{
    public abstract class CreatorBase<T>
    {
        protected readonly FieldVisitRecord FieldVisit;

        protected readonly ILog Log;

        protected CreatorBase(FieldVisitRecord record, ILog logger)
        {
            FieldVisit = record;
            Log = logger;
        }

        public abstract T Create();
    }
}
