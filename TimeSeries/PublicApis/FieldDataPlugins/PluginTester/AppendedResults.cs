using System.Collections.Generic;
using Server.BusinessInterfaces.FieldDataPluginCore.Context;

namespace PluginTester
{
    public class AppendedResults
    {
        public string FrameworkAssemblyQualifiedName { get; set; }
        public string PluginAssemblyQualifiedTypeName { get; set; }
        public List<FieldVisitInfo> AppendedVisits { get; set; } = new List<FieldVisitInfo>();
    }
}
