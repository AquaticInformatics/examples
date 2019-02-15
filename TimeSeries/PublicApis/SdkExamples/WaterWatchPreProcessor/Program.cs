using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;
using ServiceStack;
using ServiceStack.Logging.Log4Net;
using ServiceStack.Text;
using WaterWatchPreProcessor.Filters;

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
            JsConfig.EmitCamelCaseNames = true;
            JsConfig.DateHandler = DateHandler.UnixTimeMs;
        }

        public static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        }

        public static string GetExecutingFileVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // ReSharper disable once PossibleNullReferenceException
            return $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace} v{fileVersionInfo.FileVersion}";
        }

        private static Context ParseArgs(string[] args)
        {
            var context = new Context();

            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            ParseArgsIntoContext(context, resolvedArgs);

            return context;
        }

        private static void ParseArgsIntoContext(Context context, string[] resolvedArgs)
        {
            var options = new[]
            {
                new Option {Description = "https://waterwatch.io credentials"},
                new Option
                {
                    Key = nameof(context.WaterWatchOrgId),
                    Description = "WaterWatch.io organisation Id",
                    Setter = value => context.WaterWatchOrgId = value,
                    Getter = () => context.WaterWatchOrgId,
                },
                new Option
                {
                    Key = nameof(context.WaterWatchApiKey),
                    Description = "WaterWatch.io API key",
                    Setter = value => context.WaterWatchApiKey = value,
                    Getter = () => context.WaterWatchApiKey,
                },
                new Option
                {
                    Key = nameof(context.WaterWatchApiToken),
                    Description = "WaterWatch.io API token",
                    Setter = value => context.WaterWatchApiToken = value,
                    Getter = () => context.WaterWatchApiToken,
                },

                new Option(), new Option {Description = "Configuration options"},
                new Option
                {
                    Key = nameof(context.OutputMode),
                    Description =
                        $"Measurement value output mode. One of {string.Join(", ", Enum.GetNames(typeof(OutputMode)))}.",
                    Setter = value => context.OutputMode = (OutputMode) Enum.Parse(typeof(OutputMode), value, true),
                    Getter = () => context.OutputMode.ToString(),
                },
                new Option
                {
                    Key = nameof(context.SaveStatePath),
                    Description = "Path to persisted state file",
                    Setter = value => context.SaveStatePath = value,
                    Getter = () => context.SaveStatePath,
                },
                new Option
                {
                    Key = nameof(context.SyncFromUtc),
                    Description = "Optional UTC sync time. [default: last known sensor time]",
                    Setter = value => context.SyncFromUtc = ParseDateTime(value),
                },
                new Option
                {
                    Key = nameof(context.NewSensorSyncDays),
                    Description = "Number of days to sync data when a new sensor is detected.",
                    Setter = value => context.NewSensorSyncDays = int.Parse(value),
                    Getter = () => context.NewSensorSyncDays.ToString(),
                },

                new Option(), new Option {Description = "Sensor filtering options"},
                new Option
                {
                    Key = "SensorName",
                    Description = "Sensor name regular expression filter. Can be specified multiple times.",
                    Setter = value => context.SensorNameFilters.Add(ParseRegexFilter(value))
                },
                new Option
                {
                    Key = "SensorSerial",
                    Description = "Sensor serial number regular expression filter. Can be specified multiple times.",
                    Setter = value => context.SensorSerialFilters.Add(ParseRegexFilter(value))
                },
            };

            var usageMessage
                    = $"Extract the latest sensor readings from a https://waterwatch.io account"
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nSupported /{nameof(context.SyncFromUtc)} date formats:"
                      + $"\n"
                      + $"\n  {string.Join("\n  ", SupportedDateFormats)}"
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

                    if (File.Exists(arg))
                    {
                        // This is the magic which allows the preprocessor to be used in AQUARIUS DAS and EnviroSCADA 2018.1-or-earlier.
                        // Those products require that a preprocessor has one and only one argument, which is a "script file".
                        // This recursive call interprets any existing file as an @options.txt argument list.
                        // This is not necessary for EnviroSCADA 2019.1+ or Connect, since both of those allow arbitrary preprocessor command line arguments.
                        ParseArgsIntoContext(context, LoadArgsFromFile(arg).ToArray());
                        continue;
                    }

                    throw new ExpectedException($"Unknown command line argument: {arg}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o =>
                        o.Key != null && o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrWhiteSpace(context.WaterWatchOrgId)
                || string.IsNullOrEmpty(context.WaterWatchApiKey)
                || string.IsNullOrEmpty(context.WaterWatchApiToken))
                throw new ExpectedException($"Ensure your WaterWatch account credentials are set.");
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

            return LoadArgsFromFile(path);
        }

        private static IEnumerable<string> LoadArgsFromFile(string path)
        {
            if (!File.Exists(path))
                throw new ExpectedException($"Options file '{path}' does not exist.");

            return File.ReadAllLines(path)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => !s.StartsWith("#") && !s.StartsWith("//"));
        }

        private static RegexFilter ParseRegexFilter(string value)
        {
            var filter = ParseExclusionFiler(value);

            return new RegexFilter
            {
                Exclude = filter.Exclude,
                Regex = new Regex(filter.Text)
            };
        }

        private static (bool Exclude, string Text) ParseExclusionFiler(string value)
        {
            var exclude = false;
            var text = value;

            if (value.StartsWith("+"))
            {
                text = value.Substring(1);
            }
            else if (value.StartsWith("-"))
            {
                exclude = true;
                text = value.Substring(1);
            }

            return (exclude, text);
        }

        private static DateTime ParseDateTime(string value)
        {
            return DateTime.ParseExact(
                value,
                SupportedDateFormats,
                CultureInfo.InvariantCulture, 
                DateTimeStyles.AdjustToUniversal);
        }

        private static readonly string[] SupportedDateFormats =
        {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
        };
    }
}
