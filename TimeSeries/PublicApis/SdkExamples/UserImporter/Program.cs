using System;
using System.Reflection;
using ServiceStack.Logging;
using ServiceStack.Logging.Log4Net;

namespace UserImporter
{
    public class Program
    {
        private static ILog _log;
        
        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            try
            {
                ConfigureLogging();

                _log.InfoFormat("Starting user synchronization");

                var context = new UserSyncContext(args);

                var userImport = new UserImporter(context);
                userImport.Run();

                _log.InfoFormat("Done.");

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                if (_log == null)
                {
                    Console.WriteLine($"FATAL ERROR (logging not configured): {exception.Message}\n{exception.StackTrace}");
                }
                else
                {
                    _log.ErrorFormat("{0}: {1}", exception.Message, exception.StackTrace);
                }
            }
        }

        private static void ConfigureLogging()
        {
            LogManager.LogFactory = new Log4NetFactory(configureLog4Net: true);
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        }
    }
}
