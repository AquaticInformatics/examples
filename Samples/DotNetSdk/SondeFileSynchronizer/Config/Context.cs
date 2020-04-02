using System.IO;
using System.Reflection;

namespace SondeFileSynchronizer.Config
{
    public class Context
    {
        public string FolderPathToMonitor { get; set; } =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\SondeFiles";

    }
}
