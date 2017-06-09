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

                _log.Info("Starting user synchronization ...");

                var context = new UserImporterContext(args);

                var userImport = new UserImporter(context);
                userImport.Run();

                _log.Info("Done.");

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                Action<string> logAction = message => _log.Error(message);

                if (_log == null)
                {
                    logAction = message => Console.WriteLine($"FATAL ERROR (logging not configured): {message}");
                }

                if (exception is ExpectedException)
                    logAction(exception.Message);
                else
                    logAction($"{exception.Message}\n{exception.StackTrace}");
            }
        }

        private static void ConfigureLogging()
        {
            LogManager.LogFactory = new Log4NetFactory(configureLog4Net: true);
            _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        }
    }
}
