using System;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using ServiceStack;
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
                else if (exception is WebServiceException webServiceException)
                    logAction($"API: ({webServiceException.StatusCode}) {string.Join(" ", webServiceException.StatusDescription, webServiceException.ErrorCode)}: {string.Join(" ", webServiceException.Message, webServiceException.ErrorMessage)}");
                else
                    logAction($"{exception.Message}\n{exception.StackTrace}");
            }
        }

        private static void ConfigureLogging()
        {
            using (var stream = new MemoryStream(LoadEmbeddedResource("log4net.config")))
            using (var reader = new StreamReader(stream))
            {
                var xml = new XmlDocument();
                xml.LoadXml(reader.ReadToEnd());

                log4net.Config.XmlConfigurator.Configure(xml.DocumentElement);

                _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

                ServiceStack.Logging.LogManager.LogFactory = new Log4NetFactory();
            }
        }

        private static byte[] LoadEmbeddedResource(string path)
        {
            // ReSharper disable once PossibleNullReferenceException
            var resourceName = $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace}.{path}";

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new ExpectedException($"Can't load '{resourceName}' as embedded resource.");

                return stream.ReadFully();
            }
        }
    }
}
