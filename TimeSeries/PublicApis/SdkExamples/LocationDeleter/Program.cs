using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Logging.Log4Net;

namespace LocationDeleter
{
    public class Program
    {
        // ReSharper disable once InconsistentNaming
        private static ILog Log;

        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = 1;

                ConfigureLogging();

                var context = ParseArgs(args);
                new Program(context)
                    .Run();

                Environment.ExitCode = 0;
            }
            catch (ExpectedException ex)
            {
                Log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
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

                LogManager.LogFactory = new Log4NetFactory();
                Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
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
                new Option {Key = nameof(context.Server), Setter = value => context.Server = value, Getter = () => context.Server, Description = "The AQTS app server."},
                new Option {Key = nameof(context.Username), Setter = value => context.Username = value, Getter = () => context.Username, Description = "AQTS username."},
                new Option {Key = nameof(context.Password), Setter = value => context.Password = value, Getter = () => context.Password, Description = "AQTS credentials."},
                new Option {Key = nameof(context.SkipConfirmation), Setter = value => context.SkipConfirmation = bool.Parse(value), Getter = () => context.SkipConfirmation.ToString(), Description = "When true, skip the confirmation prompt. '/Y' is a shortcut for this option."},
                new Option {Key = nameof(context.DryRun), Setter = value => context.DryRun = bool.Parse(value), Getter = () => context.DryRun.ToString(), Description = "When true, don't make any changes. '/N' is a shortcut for this option."},
                new Option {Key = nameof(context.RecreateLocations), Setter = value => context.RecreateLocations = bool.Parse(value), Getter = () => context.RecreateLocations.ToString(), Description = "When true, recreate the location with the same properties."},
                new Option {Key = "Location", Setter = value => context.LocationsToDelete.Add(value), Getter = () => string.Join(", ", context.LocationsToDelete), Description = "Locations to delete."},
                new Option {Key = "TimeSeries", Setter = value => context.TimeSeriesToDelete.Add(value), Getter = () => string.Join(", ", context.TimeSeriesToDelete), Description = "Time-series to delete."},
                new Option {Key = "RatingModel", Setter = value => context.RatingModelsToDelete.Add(value), Getter = () => string.Join(", ", context.RatingModelsToDelete), Description = "Rating models to delete."},
                new Option {Key = "Visit", Setter = value => context.VisitsToDelete.Add(value), Getter = () => string.Join(", ", context.VisitsToDelete), Description = "Visits to delete."},
                new Option {Key = nameof(context.VisitsBefore), Setter = value => context.VisitsBefore = ParseDateTime(value), Getter = () => context.VisitsBefore?.ToString("O"), Description = "Delete all visits in matching locations before and including this date."},
                new Option {Key = nameof(context.VisitsAfter), Setter = value => context.VisitsAfter = ParseDateTime(value), Getter = () => context.VisitsAfter?.ToString("O"), Description = "Delete all visits in matching locations after and including this date."},
            };

            var usageMessage
                    = $"Deletes locations, time-series, rating models, and/or field visits from an AQTS server."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] [location] [time-series] [rating model] [specific-visit] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                      + $"\n"
                      + $"\nLocation deletion:"
                      + $"\n================="
                      + $"\nLocations can be specified by either location identifier or location unique ID."
                      + $"\nLocation identifiers are matched case-insensitively."
                      + $"\nPublish API wildcard expansion of '*' is supported. '02*' will match locations beginning with '02'."
                      + $"\n"
                      + $"\nTime-series deletion:"
                      + $"\n====================="
                      + $"\nTime-series can specified by identifier or by time-series unique ID."
                      + $"\n"
                      + $"\nRating model deletion:"
                      + $"\n====================="
                      + $"\nRating models can specified by identifier."
                      + $"\n"
                      + $"\nField-visit deletion:"
                      + $"\n====================="
                      + $"\nVisits can be specified by locationIdentifier@date, or locationIdentifier@dateAndTime."
                      + $"\n"
                      + $"\nWhen locationIdentifier@date is used, all visits starting on the date will be deleted."
                      + $"\nWhen locationIdentifier@dateAndTime is used, only visits starting at the exact date and time will be deleted."
                      + $"\n"
                      + $"\nWhen the /{nameof(context.VisitsBefore)}= or /{nameof(context.VisitsAfter)}= options are given, all the visits falling within the time-range will be deleted."
                      + $"\nIf no locations are specified when deleting field visits, visits from all locations will be considered."
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (arg.StartsWith("/") || arg.StartsWith("-"))
                    {
                        var keyword = arg.Substring(1);

                        if (keyword.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            context.SkipConfirmation = true;
                            continue;
                        }

                        if (keyword.Equals("n", StringComparison.InvariantCultureIgnoreCase))
                        {
                            context.DryRun = true;
                            continue;
                        }

                        throw new ExpectedException($"Unknown argument: {arg}\n\n{usageMessage}");
                    }

                    if (RatingModelIdentifier.TryParse(arg, out _))
                    {
                        context.RatingModelsToDelete.Add(arg);
                        continue;
                    }

                    if (TimeSeriesIdentifier.TryParse(arg, out _))
                    {
                        context.TimeSeriesToDelete.Add(arg);
                        continue;
                    }

                    if (VisitIdentifier.TryParse(arg, out _))
                    {
                        context.VisitsToDelete.Add(arg);
                        continue;
                    }

                    // Otherwise assume it is a location to delete
                    context.LocationsToDelete.Add(arg);
                    continue;
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option = options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    throw new ExpectedException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                option.Setter(value);
            }

            if (string.IsNullOrWhiteSpace(context.Server))
                throw new ExpectedException("No AQTS server specified. See /help or -help for details");

            if (string.IsNullOrWhiteSpace(context.Username) || string.IsNullOrWhiteSpace(context.Password))
                throw new ExpectedException("Valid AQTS credentials must be supplied.");

            if (!context.LocationsToDelete.Any()
                && !context.TimeSeriesToDelete.Any()
                && !context.RatingModelsToDelete.Any()
                && !context.VisitsToDelete.Any()
                && !context.VisitsAfter.HasValue
                && !context.VisitsBefore.HasValue)
                throw new ExpectedException($"You must specify something to delete. See /help or -help for details.");

            return context;
        }

        private static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
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

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private static DateTimeOffset ParseDateTime(string value)
        {
            if (!DateTimeOffset.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeLocal, out var dateTime))
                throw new ExpectedException($"'{value}' is not a recognized date/time string.");

            return dateTime;
        }

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new Deleter {Context = _context}
                .Run();
        }
    }
}
