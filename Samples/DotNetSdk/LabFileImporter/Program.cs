using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Aquarius.TimeSeries.Client;
using log4net;
using Humanizer;
using NodaTime;
using NodaTime.Text;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace LabFileImporter
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

                ServiceStackConfig.ConfigureServiceStack();

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

            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            var options = new[]
            {
                new Option {Description = "AQSamples connection options: (if set, imported lab results will be uploaded)"},
                new Option {Key = nameof(context.ServerUrl), Setter = value => context.ServerUrl = value, Getter = () => context.ServerUrl, Description = "AQS server URL"},
                new Option {Key = nameof(context.ApiToken), Setter = value => context.ApiToken = value, Getter = () => context.ApiToken, Description = "AQS API Token"},

                new Option(), new Option{Description = "Import options:"},
                new Option {Key = nameof(context.UtcOffset), Setter = value => context.UtcOffset = ParseOffset(value), Getter = () => string.Empty, Description = $"UTC offset for imported times [default: Use system timezone, currently {context.UtcOffset:m}]"},
                new Option {Key = nameof(context.LocationAliases).Singularize(), Setter = value => ParseLocationAlias(context, value), Description = "Set a location alias in aliasedLocation;SamplesLocationId format"},
                new Option {Key = nameof(context.ObservedPropertyAliases).Singularize(), Setter = value => ParseObservedPropertyAlias(context, value), Description = "Set an observed property alias in aliasedProperty;aliasedUnit;SamplesObservedPropertyId format"},
                new Option {Key = nameof(context.Files).Singularize(), Setter = context.Files.Add, Description = "Parse the specified CSV or XLXS as lab file results."},

                new Option(), new Option{Description = "Output options:"},
                new Option {Key = nameof(context.CsvOutputPath), Setter = value => context.CsvOutputPath = value, Getter = () => context.CsvOutputPath, Description = $"Path to output file. If not specified, no CSV will be output."},
                new Option {Key = nameof(context.Overwrite), Setter = value => context.Overwrite = ParseBoolean(value), Getter = () => $"{context.Overwrite}", Description = "Overwrite existing files?"},
            };

            var usageMessage
                    = $"Import lab file results as AQUARIUS Samples observations."
                      + $"\n"
                      + $"\nusage: {ExeHelper.ExeName} [-option=value] [@optionsFile] labFile ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
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

                    if (File.Exists(arg))
                    {
                        context.Files.Add(arg);
                        continue;
                    }

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

        private static bool ParseBoolean(string text)
        {
            if (bool.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"'{text}' is not a valid boolean value. Try {true} or {false}");
        }

        private static Offset ParseOffset(string text)
        {
            try
            {
                var offset = text.FromJson<Offset>();

                return offset;
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }

            var result = OffsetPattern.GeneralInvariantPattern.Parse(text);

            if (result.Success)
                return result.Value;

            throw new ExpectedException($"'{text}' is not a valid UTC offset {result.Exception.Message}");
        }

        private static void ParseLocationAlias(Context context, string text)
        {
            var parts = Split(text);

            if (parts.Length != 2)
                throw new ExpectedException($"'{text}' is not a valid location alias. Try /{nameof(context.LocationAliases).Singularize()}=aliasedLocation;SamplesLocationId");

            var aliasedLocation = parts[0];
            var samplesLocationId = parts[1];

            if (context.LocationAliases.TryGetValue(aliasedLocation, out var existingAlias))
                throw new ExpectedException($"Can't set location alias for '{aliasedLocation}' more than once. This location is already aliased to '{existingAlias}'");

            context.LocationAliases[aliasedLocation] = samplesLocationId;
        }

        private static void ParseObservedPropertyAlias(Context context, string text)
        {
            var parts = Split(text);

            if (parts.Length != 3)
                throw new ExpectedException($"'{text}' is not a valid observed property alias. Try /{nameof(context.ObservedPropertyAliases).Singularize()}=aliasedProperty;aliasedUnit;SamplesObservedPropertyId");

            var aliasedProperty = parts[0];
            var aliasedUnit = parts[1];
            var samplesObservedPropertyId = parts[2];

            var key = $"{aliasedProperty}:{aliasedUnit}";

            if (context.LocationAliases.TryGetValue(key, out var existingAlias))
                throw new ExpectedException($"Can't set observed property alias for '{key}' more than once. This property:unit is already aliased to '{existingAlias}'");

            context.ObservedPropertyAliases[aliasedProperty] = samplesObservedPropertyId;
        }

        private static string[] Split(string text)
        {
            return text
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new Importer
                {
                    Context = _context
                }
                .Import();
        }
    }
}
