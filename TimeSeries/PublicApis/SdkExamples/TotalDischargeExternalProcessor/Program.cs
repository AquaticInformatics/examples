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

namespace TotalDischargeExternalProcessor
{
    class Program
    {
        private static ILog _log;

        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            try
            {
                ConfigureLogging();

                var context = ParseArgs(args);
                new Program(context).Run();

                Environment.ExitCode = 0;
            }
            catch (WebServiceException exception)
            {
                _log.Error($"API: ({exception.StatusCode}) {string.Join(" ", exception.StatusDescription, exception.ErrorCode)}: {string.Join(" ", exception.Message, exception.ErrorMessage)}", exception);
            }
            catch (ExpectedException exception)
            {
                _log.Error(exception.Message);
            }
            catch (Exception exception)
            {
                _log.Error("Unhandled exception", exception);
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

        private static Context ParseArgs(string[] args)
        {
            var context = new Context();

            var resolvedArgs = InjectOptionsFileByDefault(args)
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
                    Key = nameof(context.ConfigPath),
                    Setter = value => context.ConfigPath = value,
                    Getter = () => context.ConfigPath,
                    Description = $"Path to the JSON configuration file. [default: '{nameof(Config)}.json' in the same folder as the EXE]"
                },
                new Option
                {
                    Key = nameof(context.CreateMissingTimeSeries),
                    Setter = value => context.CreateMissingTimeSeries = bool.Parse(value),
                    Getter = () => $"{context.CreateMissingTimeSeries}",
                    Description = "When true, any missing time-series will be created."
                },
            };

            var usageMessage
                    = $"An external processor for calculating total discharge for arbitrary-length events."
                      + $"\n"
                      + $"\nusage: {ExeHelper.ExeName} [-option=value] [@optionsFile] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nWhen no other command line options are given, the Options.txt file in"
                      + $"\nsame folder as the EXE will be used if it exists."
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                ;

            var helpGuidance = "See /help screen for details.";

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (HelpKeyWords.Contains(arg))
                        throw new ExpectedException(usageMessage);

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{helpGuidance}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key != null && o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{helpGuidance}");
                }

                option.Setter(value);
            }

            return context;
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private static readonly HashSet<string> HelpKeyWords =
            new HashSet<string>(
                new[] { "?", "h", "help" }
                    .SelectMany(keyword => new[] { "/", "-", "--" }.Select(prefix => prefix + keyword)),
                StringComparer.InvariantCultureIgnoreCase);

        private static string[] InjectOptionsFileByDefault(string[] args)
        {
            if (args.Any())
                return args;

            var defaultOptionsFile = Path.Combine(ExeHelper.ExeDirectory, "Options.txt");

            if (File.Exists(defaultOptionsFile))
            {
                _log.Info($"Using '@{defaultOptionsFile}' configuration by default.");

                return new[] { "@" + defaultOptionsFile };
            }

            return args;
        }

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

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            _log.Info(ExeHelper.ExeNameAndVersion);

            new ExternalProcessor
            {
                Context = _context
            }.Run();
        }
    }
}
