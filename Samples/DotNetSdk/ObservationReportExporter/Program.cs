using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Aquarius.Samples.Client;
using Aquarius.TimeSeries.Client;
using Humanizer;
using log4net;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace ObservationReportExporter
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
            catch (SamplesApiException exception)
            {
                _log.Error(
                    $"API: ({exception.StatusCode}) - {exception.SamplesError?.ErrorCode}: {exception.SamplesError?.Message}");
            }
            catch (WebServiceException exception)
            {
                _log.Error(
                    $"API: ({exception.StatusCode}) {string.Join(" ", exception.StatusDescription, exception.ErrorCode)}: {string.Join(" ", exception.Message, exception.ErrorMessage)}",
                    exception);
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

                // ReSharper disable once PossibleNullReferenceException
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
                new Option { Description = "AQUARIUS Samples connection options:" },
                new Option
                {
                    Key = nameof(context.SamplesServer),
                    Setter = value => context.SamplesServer = value,
                    Getter = () => context.SamplesServer,
                    Description = "AQS server URL"
                },
                new Option
                {
                    Key = nameof(context.SamplesApiToken),
                    Setter = value => context.SamplesApiToken = value,
                    Getter = () => context.SamplesApiToken,
                    Description = "AQS API Token"
                },

                new Option(), new Option { Description = "AQUARIUS Time-Series connection options:" },
                new Option
                {
                    Key = nameof(context.TimeSeriesServer),
                    Setter = value => context.TimeSeriesServer = value,
                    Getter = () => context.TimeSeriesServer,
                    Description = "AQTS server"
                },
                new Option
                {
                    Key = nameof(context.TimeSeriesUsername),
                    Setter = value => context.TimeSeriesUsername = value,
                    Getter = () => context.TimeSeriesUsername,
                    Description = "AQTS username"
                },
                new Option
                {
                    Key = nameof(context.TimeSeriesPassword),
                    Setter = value => context.TimeSeriesPassword = value,
                    Getter = () => context.TimeSeriesPassword,
                    Description = "AQTS password"
                },

                new Option(), new Option { Description = "Export options:" },
                new Option
                {
                    Key = nameof(context.ExportTemplateName),
                    Setter = value => context.ExportTemplateName = value,
                    Getter = () => context.ExportTemplateName,
                    Description = "The Observation Export Spreadsheet Template to use for all exports."
                },
                new Option
                {
                    Key = nameof(context.AttachmentFilename),
                    Setter = value => context.AttachmentFilename = value,
                    Getter = () => context.AttachmentFilename,
                    Description = "Filename of the exported attachment"
                },
                new Option
                {
                    Key = nameof(context.AttachmentTags),
                    Setter = value => ParseAttachmentTagValue(context, value),
                    Getter = () => string.Empty,
                    Description = "Uploaded attachments will have these tag values applies, in key:value format."
                },
                new Option
                {
                    Key = nameof(context.DeleteExistingAttachments),
                    Setter = value => context.DeleteExistingAttachments = ParseBoolean(value),
                    Getter = () => $"{context.DeleteExistingAttachments}",
                    Description = "Delete any existing location attachments with the same name."
                },
                new Option
                {
                    Key = nameof(context.DryRun),
                    Setter = value => context.DryRun = ParseBoolean(value),
                    Getter = () => $"{context.DryRun}",
                    Description = "When true, don't export and upload reports, just validate what would be done."
                },

                new Option(),
                new Option
                {
                    Description = "Cumulative filter options: (ie. AND-ed together). Can be set multiple times."
                },
                new Option
                {
                    Key = nameof(context.StartTime),
                    Setter = value => context.StartTime = ParseDateTimeOffset(value),
                    Getter = () => string.Empty,
                    Description = "Include observations after this time."
                },
                new Option
                {
                    Key = nameof(context.EndTime),
                    Setter = value => context.EndTime = ParseDateTimeOffset(value),
                    Getter = () => string.Empty,
                    Description = "Include observations before this time."
                },
                new Option
                {
                    Key = nameof(context.LocationIds).Singularize(),
                    Setter = value => ParseListItem(context.LocationIds, value),
                    Getter = () => string.Empty,
                    Description = "Observations matching these sampling locations."
                },
                new Option
                {
                    Key = nameof(context.LocationGroupIds).Singularize(),
                    Setter = value => ParseListItem(context.LocationGroupIds, value),
                    Getter = () => string.Empty,
                    Description = "Observations matching these sampling location groups."
                },
                new Option
                {
                    Key = nameof(context.AnalyticalGroupIds).Singularize(),
                    Setter = value => ParseListItem(context.AnalyticalGroupIds, value),
                    Getter = () => string.Empty,
                    Description = "Observations matching these analytical groups."
                },
                new Option
                {
                    Key = nameof(context.ObservedPropertyIds).Singularize(),
                    Setter = value => ParseListItem(context.ObservedPropertyIds, value),
                    Getter = () => string.Empty,
                    Description = "Observations matching these observed properties."
                },
            };

            var usageMessage
                    = $"Export observations from AQUARIUS Samples using a spreadsheet template and into AQUARIUS Time-Series."
                      + $"\n"
                      + $"\nusage: {ExeHelper.ExeName} [-option=value] [@optionsFile] ..."
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

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{helpGuidance}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o =>
                        o.Key != null && o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{helpGuidance}");
                }

                option.Setter(value);
            }

            return context;
        }

        private static readonly Regex
            ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

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

        private static DateTimeOffset ParseDateTimeOffset(string text)
        {
            if (DateTimeOffset.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"'{text}' is not a valid date-time. Try yyyy-MM-dd or yyyy-MM-dd HH:mm:ss");
        }

        private static bool ParseBoolean(string text)
        {
            if (bool.TryParse(text, out var value))
                return value;

            throw new ExpectedException($"'{text}' is not a valid boolean value. Try {true} or {false}");
        }

        private static void ParseListItem(List<string> list, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            list.Add(text);
        }

        private static void ParseAttachmentTagValue(Context context, string text)
        {
            var components = text.Split(TagValueSeparators, 2)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (!components.Any())
                throw new ExpectedException($"'{text}' is not in the key:value format.");

            context.AttachmentTags[components[0]] = components.Count < 2
                ? string.Empty
                : components[1];
        }

        private static readonly char[] TagValueSeparators = { ':' };

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new Exporter
                {
                    Context = _context
                }
                .Run();
        }
    }
}
