using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace WaterWatchPreProcessor
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
                ConfigureJson();

                var context = ParseArgs(args);

                new Exporter { Context = context }
                    .Run();

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
            using (var stream = new MemoryStream(EmbeddedResource.LoadEmbeddedResource("log4net.config")))
            using (var reader = new StreamReader(stream))
            {
                var xml = new XmlDocument();
                xml.LoadXml(reader.ReadToEnd());

                log4net.Config.XmlConfigurator.Configure(xml.DocumentElement);

                _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

                ServiceStack.Logging.LogManager.LogFactory = new Log4NetFactory();
            }
        }

        private static void ConfigureJson()
        {
        }

        private static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        }

        private static Context ParseArgs(string[] args)
        {
            var context = new Context();

            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            var options = new[]
            {
                new Option {Key = nameof(context.WaterWaterOrgId), Setter = value => context.WaterWaterOrgId = value, Getter = () => context.WaterWaterOrgId, Description = "WaterWatch.io organisation Id"},
                new Option {Key = nameof(context.WaterWaterApiKey), Setter = value => context.WaterWaterApiKey = value, Getter = () => context.WaterWaterApiKey, Description = "WaterWatch.io API key"},
                new Option {Key = nameof(context.WaterWaterApiToken), Setter = value => context.WaterWaterApiToken = value, Getter = () => context.WaterWaterApiToken, Description = "WaterWatch.io API token"},
                new Option {Key = nameof(context.SaveStatePath), Setter = value => context.SaveStatePath = value, Getter = () => context.SaveStatePath, Description = "Path to persisted state file"},
            };

            var usageMessage
                    = $"Extract the latest sensor readings from a https://waterwatch.io account"
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace are ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (HelpKeywords.Contains(arg))
                        throw new ExpectedException($"Showing help page\n\n{usageMessage}");

                    throw new ExpectedException($"Unknown command line argument: {arg}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key != null && o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrWhiteSpace(context.WaterWaterOrgId)
                || string.IsNullOrEmpty(context.WaterWaterApiKey)
                || string.IsNullOrEmpty(context.WaterWaterApiToken))
                throw new ExpectedException($"Ensure your WaterWatch account credentials are set.");

            return context;
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private static readonly HashSet<string> HelpKeywords =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "-help", "--help", "-h", "/h", "/help",
                "-?", "/?",
            };

        private static IEnumerable<string> ResolveOptionsFromFile(string arg)
        {
            if (!arg.StartsWith("@"))
                return new[] { arg };

            var path = arg.Substring(1);

            if (!File.Exists(path))
                throw new ExpectedException($"Options file '{path}' does not exist.");

            return File.ReadAllLines(path)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !s.StartsWith("#") && !s.StartsWith("//"));
        }

    }
}
