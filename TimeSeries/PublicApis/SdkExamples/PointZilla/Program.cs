using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using log4net;
using NodaTime;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace PointZilla
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
            var resourceName = $"{GetProgramName()}.{path}";

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
                new Option {Key = nameof(context.Server), Setter = value => context.Server = value, Getter = () => context.Server, Description = "AQTS server name"},
                new Option {Key = nameof(context.Username), Setter = value => context.Username = value, Getter = () => context.Username, Description = "AQTS username"},
                new Option {Key = nameof(context.Password), Setter = value => context.Password = value, Getter = () => context.Password, Description = "AQTS password"},

                new Option {Key = nameof(context.Wait), Setter = value => context.Wait = bool.Parse(value), Getter = () => context.Wait.ToString(), Description = "Wait for the append request to complete"},
                new Option {Key = nameof(context.AppendTimeout), Setter = value => context.AppendTimeout = TimeSpan.Parse(value), Getter = () => context.AppendTimeout.ToString(), Description = "Timeout period for append completion"},

                new Option {Key = nameof(context.TimeSeries), Setter = value => context.TimeSeries = value, Getter = () => context.TimeSeries, Description = "Time-series identifier or unique ID"},
                new Option {Key = nameof(context.TimeRange), Setter = value => context.TimeRange = ParseInterval(value), Getter = () => context.TimeRange?.ToString(), Description = "Time-range for overwrite (defaults to start/end points)"},
                new Option {Key = nameof(context.Command), Setter = value => context.Command = (CommandType)Enum.Parse(typeof(CommandType), value, true), Getter = () => context.Command.ToString(), Description = $"Append operation to perform. One of {string.Join(", ", Enum.GetNames(typeof(CommandType)))}"},
                new Option {Key = nameof(context.GradeCode), Setter = value => context.GradeCode = int.Parse(value), Getter = () => context.GradeCode.ToString(), Description = "Optional grade code for all appended points"},
                new Option {Key = nameof(context.Qualifiers), Setter = value => context.Qualifiers = QualifiersParser.Parse(value), Getter = () => string.Join(",", context.Qualifiers), Description = "Optional qualifier list for all appended points"},

                new Option {Key = nameof(context.SourceTimeSeries), Setter = value => {if (TimeSeriesIdentifier.TryParse(value, out var tsi)) context.SourceTimeSeries = tsi; }, Getter = () => context.SourceTimeSeries?.ToString(), Description = "Source time-series to copy. Prefix with server2[:username2:password2]: to copy from another server"},
                new Option {Key = nameof(context.SourceQueryFrom), Setter = value => context.SourceQueryFrom = value.FromJson<Instant>(), Getter = () => context.SourceQueryFrom?.ToJson(), Description = "Start time of extracted points"},
                new Option {Key = nameof(context.SourceQueryTo), Setter = value => context.SourceQueryTo = value.FromJson<Instant>(), Getter = () => context.SourceQueryTo?.ToJson(), Description = "End time of extracted points"},

                new Option {Key = nameof(context.StartTime), Setter = value => context.StartTime = value.FromJson<Instant>(), Getter = () => context.StartTime.ToJson(), Description = "Start time of generated points."},
                new Option {Key = nameof(context.PointInterval), Setter = value => context.PointInterval = TimeSpan.Parse(value), Getter = () => context.PointInterval.ToString(), Description = "Interval between generated points"},
                new Option {Key = nameof(context.NumberOfPoints), Setter = value => context.NumberOfPoints = int.Parse(value), Getter = () => context.NumberOfPoints.ToString(), Description = $"Number of points to generate. If 0, use {nameof(context.NumberOfPeriods)}"},
                new Option {Key = nameof(context.NumberOfPeriods), Setter = value => context.NumberOfPeriods = double.Parse(value), Getter = () => context.NumberOfPeriods.ToString(CultureInfo.InvariantCulture), Description = "Number of periods"},
                new Option {Key = nameof(context.FunctionType), Setter = value => context.FunctionType = (FunctionType)Enum.Parse(typeof(FunctionType), value, true), Getter = () => context.FunctionType.ToString(), Description = $"Function to generate. One of {string.Join(", ", Enum.GetNames(typeof(FunctionType)))}"},
                new Option {Key = nameof(context.FunctionOffset), Setter = value => context.FunctionOffset = double.Parse(value), Getter = () => context.FunctionOffset.ToString(CultureInfo.InvariantCulture), Description = "Function offset"},
                new Option {Key = nameof(context.FunctionPhase), Setter = value => context.FunctionPhase = double.Parse(value), Getter = () => context.FunctionPhase.ToString(CultureInfo.InvariantCulture), Description = "Function phase"},
                new Option {Key = nameof(context.FunctionScalar), Setter = value => context.FunctionScalar = double.Parse(value), Getter = () => context.FunctionScalar.ToString(CultureInfo.InvariantCulture), Description = "Function scalar"},
                new Option {Key = nameof(context.FunctionPeriod), Setter = value => context.FunctionPeriod = double.Parse(value), Getter = () => context.FunctionPeriod.ToString(CultureInfo.InvariantCulture), Description = "Function period"},

                new Option {Key = "CSV", Setter = value => context.CsvFiles.Add(value), Getter = () => string.Join(", ", context.CsvFiles), Description = "Parse the CSV file"},
                new Option {Key = nameof(context.CsvTimeField), Setter = value => context.CsvTimeField = int.Parse(value), Getter = () => context.CsvTimeField.ToString(), Description = "CSV column index for timestamps"},
                new Option {Key = nameof(context.CsvValueField), Setter = value => context.CsvValueField = int.Parse(value), Getter = () => context.CsvValueField.ToString(), Description = "CSV column index for values"},
                new Option {Key = nameof(context.CsvGradeField), Setter = value => context.CsvGradeField = int.Parse(value), Getter = () => context.CsvGradeField.ToString(), Description = "CSV column index for grade codes"},
                new Option {Key = nameof(context.CsvQualifiersField), Setter = value => context.CsvQualifiersField = int.Parse(value), Getter = () => context.CsvQualifiersField.ToString(), Description = "CSV column index for qualifiers"},
                new Option {Key = nameof(context.CsvTimeFormat), Setter = value => context.CsvTimeFormat = value, Getter = () => context.CsvTimeFormat, Description = "Format of CSV time fields (defaults to ISO8601)"},
                new Option {Key = nameof(context.CsvComment), Setter = value => context.CsvComment = value, Getter = () => context.CsvComment, Description = "CSV commment lines begin with this prefix"},
                new Option {Key = nameof(context.CsvSkipRows), Setter = value => context.CsvSkipRows = int.Parse(value), Getter = () => context.CsvSkipRows.ToString(), Description = "Number of CSV rows to skip before parsing"},
                new Option {Key = nameof(context.CsvIgnoreInvalidRows), Setter = value => context.CsvIgnoreInvalidRows = bool.Parse(value), Getter = () => context.CsvIgnoreInvalidRows.ToString(), Description = "Ignore CSV rows that can't be parsed"},
                new Option {Key = nameof(context.CsvRealign), Setter = value => context.CsvRealign = bool.Parse(value), Getter = () => context.CsvRealign.ToString(), Description = $"Realign imported CSV points to the /{nameof(context.StartTime)} value"},
            };

            var usageMessage
                    = $"Append points to an AQTS time-series."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] [command] [identifierOrGuid] [value] [csvFile]"
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                ;

            var knownCommands = new Dictionary<string, CommandType>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var commandType in Enum.GetValues(typeof(CommandType)).Cast<CommandType>())
            {
                knownCommands.Add(commandType.ToString(), commandType);
            }

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    // Try positional arguments: [command] [identifierOrGuid] [value] [csvFile]
                    if (knownCommands.TryGetValue(arg, out var command))
                    {
                        context.Command = command;
                        continue;
                    }

                    if (double.TryParse(arg, out var numericValue))
                    {
                        ParseManualPoints(context, numericValue);
                        continue;
                    }

                    if (File.Exists(arg))
                    {
                        context.CsvFiles.Add(arg);
                        continue;
                    }

                    if (Guid.TryParse(arg, out var _))
                    {
                        context.TimeSeries = arg;
                        continue;
                    }

                    if (TimeSeriesIdentifier.TryParse(arg, out var _))
                    {
                        context.TimeSeries = arg;
                        continue;
                    }

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{usageMessage}");
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

            if (string.IsNullOrWhiteSpace(context.TimeSeries))
                throw new ExpectedException($"A /{nameof(context.TimeSeries)} option is required.\n\n{usageMessage}");

            return context;
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);
        
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


        private static void ParseManualPoints(Context context, double numericValue)
        {
            context.ManualPoints.Add(new ReflectedTimeSeriesPoint
            {
                Time = context.StartTime,
                Value = numericValue,
                GradeCode = context.GradeCode,
                Qualifiers = context.Qualifiers
            });

            context.StartTime = context.StartTime.Plus(Duration.FromTimeSpan(context.PointInterval));
        }

        private static Interval ParseInterval(string text)
        {
            var components = text.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            if (components.Length != 2)
                throw new ExpectedException($"'{text}' is an invalid Interval format. Use 'StartInstant/EndInstant' (two ISO8601 timestamps, separated by a forward slash)");

            return new Interval(
                components[0].FromJson<Instant>(),
                components[1].FromJson<Instant>());
        }

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new PointsAppender(_context)
                .AppendPoints();
        }
    }
}
