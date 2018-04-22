using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;
using NodaTime.Text;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace TimeSeriesChangeMonitor
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

                var context = ParseArgs(args);

                new Monitor {Context = context}
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
                    logAction($"API: ({webServiceException.StatusCode}) {webServiceException.ErrorCode}: {webServiceException.ErrorMessage}");
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
                new Option
                {
                    Key = nameof(context.Server),
                    Setter = value => context.Server = value,
                    Getter = () => context.Server,
                    Description = "AQTS server name"
                },
                new Option
                {
                    Key = nameof(context.Username),
                    Setter = value => context.Username = value,
                    Getter = () => context.Username,
                    Description = "AQTS username"
                },
                new Option
                {
                    Key = nameof(context.Password),
                    Setter = value => context.Password = value,
                    Getter = () => context.Password,
                    Description = "AQTS password"
                },
                new Option
                {
                    Key = nameof(context.LocationIdentifier),
                    Setter = value => context.LocationIdentifier = value,
                    Getter = () => context.LocationIdentifier,
                    Description = "Optional location filter."
                },
                new Option
                {
                    Key = nameof(context.Publish),
                    Setter = value => context.Publish = bool.Parse(value),
                    Getter = () => context.Publish?.ToString(),
                    Description = "Optional publish filter."
                },
                new Option
                {
                    Key = nameof(context.ChangeEventType),
                    Setter = value => context.ChangeEventType = (ChangeEventType) Enum.Parse(typeof(ChangeEventType), value, true),
                    Getter = () => context.ChangeEventType?.ToString(),
                    Description = $"Optional change event type filter. One of {string.Join(", ", Enum.GetNames(typeof(ChangeEventType)))}"
                },
                new Option
                {
                    Key = nameof(context.Parameter),
                    Setter = value => context.Parameter = value,
                    Getter = () => context.Parameter,
                    Description = "Optional parameter filter."
                },
                new Option
                {
                    Key = nameof(context.ComputationIdentifier),
                    Setter = value => context.ComputationIdentifier = value,
                    Getter = () => context.ComputationIdentifier,
                    Description = "Optional computation filter."
                },
                new Option
                {
                    Key = nameof(context.ComputationPeriodIdentifier),
                    Setter = value => context.ComputationPeriodIdentifier = value,
                    Getter = () => context.ComputationPeriodIdentifier,
                    Description = "Optional computation period filter."
                },
                new Option
                {
                    Key = nameof(context.ExtendedFilters),
                    Setter = value =>
                    {
                        var split = value.Split('=');

                        if (split.Length != 2)
                            throw new ExpectedException($"Can't parse '{value}' as Name=Value extended attribute filter");

                        context.ExtendedFilters.Add(new ExtendedAttributeFilter
                        {
                            FilterName = split[0],
                            FilterValue = split[1]
                        });
                    },
                    Getter = () => string.Empty,
                    Description = "Optional extended attribute filter in Name=Value format. Can be set multiple times."
                },
                new Option
                {
                    Key = nameof(context.TimeSeries),
                    Setter = value => context.TimeSeries.Add(value),
                    Getter = () => string.Empty,
                    Description = "Optional time-series to monitor. Can be set multiple times."
                },
                new Option
                {
                    Key = nameof(context.ChangesSinceTime),
                    Setter = value => context.ChangesSinceTime = InstantPattern.ExtendedIsoPattern.Parse(value).GetValueOrThrow(),
                    Getter = () => string.Empty,
                    Description = "The starting changes-since time in ISO 8601 format. Defaults to 'right now'"
                },
                new Option
                {
                    Key = nameof(context.PollInterval),
                    Setter = value => context.PollInterval = value.ToUpperInvariant().ParseDuration(),
                    Getter = () => context.PollInterval.SerializeToString(),
                    Description = "The polling interval in ISO 8601 Duration format."
                },
                new Option
                {
                    Key = nameof(context.MaximumChangeCount),
                    Setter = value => context.MaximumChangeCount = int.Parse(value),
                    Getter = () => context.MaximumChangeCount.ToString(),
                    Description = "When greater than 0, exit after detecting this many changed time-series."
                },
                new Option
                {
                    Key = nameof(context.AllowQuickPolling),
                    Setter = value => context.AllowQuickPolling = bool.Parse(value),
                    Getter = () => context.AllowQuickPolling.ToString(),
                    Description = "Allows very quick polling. Good for testing, bad for production."
                },
            };

            var usageMessage
                    = $"Monitor time-series changes in an AQTS time-series."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] [location] [timeSeriesIdentifierOrGuid] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nISO 8601 timestamps use a yyyy'-'mm'-'dd'T'HH':'mm':'ss'.'fffffffzzz format."
                      + $"\n"
                      + $"\n  The 7 fractional seconds digits are optional."
                      + $"\n  The zzz timezone can be 'Z' for UTC, or +HH:MM, or -HH:MM"
                      + $"\n"
                      + $"\n  Eg: 2017-04-01T00:00:00Z represents April 1st, 2017 in UTC."
                      + $"\n"
                      + $"\nISO 8601 durations use a 'PT'[nnH][nnM][nnS] format."
                      + $"\n"
                      + $"\n  Only the required components are needed."
                      + $"\n"
                      + $"\n  Eg: PT5M represents 5 minutes."
                      + $"\n      PT90S represents 90 seconds (1.5 minutes)"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (HelpKeywords.Contains(arg))
                        throw new ExpectedException($"Showing help page\n\n{usageMessage}");

                    // Try positional arguments: [locationIdentifier] [timeSeriesIdentifier]
                    if (Guid.TryParse(arg, out var _))
                    {
                        context.TimeSeries.Add(arg);
                        continue;
                    }

                    if (TimeSeriesIdentifier.TryParse(arg, out var _))
                    {
                        context.TimeSeries.Add(arg);
                        continue;
                    }

                    context.LocationIdentifier = arg;
                    continue;
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrWhiteSpace(context.Server))
                throw new ExpectedException($"A /{nameof(context.Server)} option is required.\n\n{usageMessage}");

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
