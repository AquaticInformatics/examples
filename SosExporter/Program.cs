using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using log4net;
using ServiceStack;
using ServiceStack.Logging.Log4Net;

namespace SosExporter
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
            ServiceStackConfig.ConfigureServiceStack();
            SosClient.ConfigureJsonOnce();
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
                new Option{Description = "Export configuration settings. Changes will trigger a full resync:"},
                new Option
                {
                    Key = nameof(context.Config.AquariusServer),
                    Setter = value => context.Config.AquariusServer = value,
                    Getter = () => context.Config.AquariusServer,
                    Description = "AQTS server name"
                },
                new Option
                {
                    Key = nameof(context.Config.AquariusUsername),
                    Setter = value => context.Config.AquariusUsername = value,
                    Getter = () => context.Config.AquariusUsername,
                    Description = "AQTS username"
                },
                new Option
                {
                    Key = nameof(context.Config.AquariusPassword),
                    Setter = value => context.Config.AquariusPassword = value,
                    Getter = () => context.Config.AquariusPassword,
                    Description = "AQTS password"
                },
                new Option
                {
                    Key = nameof(context.Config.SosServer),
                    Setter = value => context.Config.SosServer = value,
                    Getter = () => context.Config.SosServer,
                    Description = "SOS server name"
                },
                new Option
                {
                    Key = nameof(context.Config.SosUsername),
                    Setter = value => context.Config.SosUsername = value,
                    Getter = () => context.Config.SosUsername,
                    Description = "SOS username"
                },
                new Option
                {
                    Key = nameof(context.Config.SosPassword),
                    Setter = value => context.Config.SosPassword = value,
                    Getter = () => context.Config.SosPassword,
                    Description = "SOS password"
                },

                new Option(), new Option{Description = "/Publish/v2/GetTimeDescriptionList settings. Changes will trigger a full resync:"}, 
                new Option
                {
                    Key = nameof(context.Config.LocationIdentifier),
                    Setter = value => context.Config.LocationIdentifier = value,
                    Getter = () => context.Config.LocationIdentifier,
                    Description = "Optional location filter."
                },
                new Option
                {
                    Key = nameof(context.Config.Publish),
                    Setter = value => context.Config.Publish = string.IsNullOrEmpty(value) ? (bool?)null : bool.Parse(value),
                    Getter = () => context.Config.Publish?.ToString(),
                    Description = "Optional publish filter."
                },
                new Option
                {
                    Key = nameof(context.Config.ChangeEventType),
                    Setter = value => context.Config.ChangeEventType = (ChangeEventType) Enum.Parse(typeof(ChangeEventType), value, true),
                    Getter = () => context.Config.ChangeEventType?.ToString(),
                    Description = $"Optional change event type filter. One of {string.Join(", ", Enum.GetNames(typeof(ChangeEventType)))}"
                },
                new Option
                {
                    Key = nameof(context.Config.Parameter),
                    Setter = value => context.Config.Parameter = value,
                    Getter = () => context.Config.Parameter,
                    Description = "Optional parameter filter."
                },
                new Option
                {
                    Key = nameof(context.Config.ComputationIdentifier),
                    Setter = value => context.Config.ComputationIdentifier = value,
                    Getter = () => context.Config.ComputationIdentifier,
                    Description = "Optional computation filter."
                },
                new Option
                {
                    Key = nameof(context.Config.ComputationPeriodIdentifier),
                    Setter = value => context.Config.ComputationPeriodIdentifier = value,
                    Getter = () => context.Config.ComputationPeriodIdentifier,
                    Description = "Optional computation period filter."
                },
                new Option
                {
                    Key = nameof(context.Config.ExtendedFilters),
                    Setter = value =>
                    {
                        var split = value.Split('=');

                        if (split.Length != 2)
                            throw new ExpectedException($"Can't parse '{value}' as Name=Value extended attribute filter");

                        context.Config.ExtendedFilters.Add(new ExtendedAttributeFilter
                        {
                            FilterName = split[0],
                            FilterValue = split[1]
                        });
                    },
                    Getter = () => string.Empty,
                    Description = "Extended attribute filter in Name=Value format. Can be set multiple times."
                },

                new Option(), new Option{Description = "Aggressive time-series filtering. Changes will trigger a full resync:"}, 
                new Option
                {
                    Key = nameof(context.Config.TimeSeries),
                    Setter = value => context.Config.TimeSeries.Add(ParseTimeSeriesFilter(value)),
                    Getter = () => string.Empty,
                    Description = "Time-series identifier regular expression filter. Can be specified multiple times."
                },
                new Option
                {
                    Key = nameof(context.Config.Approvals),
                    Setter = value => context.Config.Approvals.Add(ParseApprovalFilter(value)),
                    Getter = () => string.Empty,
                    Description = "Filter points by approval level or name. Can be specified multiple times."
                },
                new Option
                {
                    Key = nameof(context.Config.Grades),
                    Setter = value => context.Config.Grades.Add(ParseGradeFilter(value)),
                    Getter = () => string.Empty,
                    Description = "Filter points by grade code or name. Can be specified multiple times."
                },
                new Option
                {
                    Key = nameof(context.Config.Qualifiers),
                    Setter = value => context.Config.Qualifiers.Add(ParseQualifierFilter(value)),
                    Getter = () => string.Empty,
                    Description = "Filter points by qualifier. Can be specified multiple times."
                },

                new Option(),
                new Option {Description = "Maximum time range of points to upload: Changes will trigger a full resync:"},
                new Option
                {
                    Key = nameof(context.Config.MaximumPointDays),
                    Setter = value =>
                    {
                        var components = value.Split('=');
                        if (components.Length == 2)
                        {
                            var periodText = components[0];
                            var daysText = components[1];

                            var period = (ComputationPeriod) Enum.Parse(typeof(ComputationPeriod), periodText, true);
                            var days = daysText.Equals("All", StringComparison.InvariantCultureIgnoreCase)
                                ? -1
                                : int.Parse(daysText);

                            context.Config.MaximumPointDays[period] = days;
                        }
                    },
                    Getter = () => "\n    " + string.Join("\n    ", context.Config.MaximumPointDays.Select(kvp =>
                    {
                        var daysText = kvp.Value > 0
                            ? kvp.Value.ToString()
                            : "All";

                        return $"{kvp.Key,-8} = {daysText}";
                    })) + "\n  ",
                    Description = "Days since the last point to upload, in Frequency=Value format."
                },

                new Option(), 
                new Option {Description = "Other options: (Changing these values won't trigger a full resync)"},
                new Option
                {
                    Key = nameof(context.ConfigurationName),
                    Setter = value => context.ConfigurationName = value,
                    Getter = () => context.ConfigurationName,
                    Description = "The name of the export configuration, to be saved in the AQTS global settings."
                },
                new Option
                {
                    Key = nameof(context.DryRun),
                    Setter = value => context.DryRun = bool.Parse(value),
                    Getter = () => context.DryRun.ToString(),
                    Description = "When true, don't export to SOS. Only log the changes that would have been performed."
                },
                new Option
                {
                    Key = nameof(context.ForceResync),
                    Setter = value => context.ForceResync = bool.Parse(value),
                    Getter = () => context.ForceResync.ToString(),
                    Description = "When true, force a full resync of all time-series."
                },
                new Option
                {
                    Key = nameof(context.NeverResync),
                    Setter = value => context.NeverResync = bool.Parse(value),
                    Getter = () => context.NeverResync.ToString(),
                    Description = "When true, avoid full time-series resync, even when the algorithm recommends it."
                },
                new Option
                {
                    Key = nameof(context.ChangesSince),
                    Setter = value => context.ChangesSince = DateTimeOffset.Parse(value),
                    Getter = () => string.Empty,
                    Description = "The starting changes-since time in ISO 8601 format. Defaults to the saved AQTS global setting value."
                },
                new Option
                {
                    Key = nameof(context.MaximumPointsPerObservation),
                    Setter = value => context.MaximumPointsPerObservation = int.Parse(value),
                    Getter = () => context.MaximumPointsPerObservation.ToString(),
                    Description = "The maximum number of points per SOS observation"
                }
            };

            var usageMessage
                    = $"Export time-series changes in AQTS time-series to an OGC SOS server."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nISO 8601 timestamps use a yyyy'-'mm'-'dd'T'HH':'mm':'ss'.'fffffffzzz format."
                      + $"\n"
                      + $"\n  The 7 fractional seconds digits are optional."
                      + $"\n  The zzz timezone can be 'Z' for UTC, or +HH:MM, or -HH:MM"
                      + $"\n"
                      + $"\n  Eg: 2017-04-01T00:00:00Z represents April 1st, 2017 in UTC."
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

            if (string.IsNullOrWhiteSpace(context.Config.AquariusServer)
                || string.IsNullOrEmpty(context.Config.AquariusUsername)
                || string.IsNullOrEmpty(context.Config.AquariusPassword))
                throw new ExpectedException($"Ensure your AQTS server credentials are set.");

            if (string.IsNullOrWhiteSpace(context.Config.SosServer)
                || string.IsNullOrEmpty(context.Config.SosUsername)
                || string.IsNullOrEmpty(context.Config.SosPassword))
                throw new ExpectedException($"Ensure your SOS server credentials are set.");

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

        private static TimeSeriesFilter ParseTimeSeriesFilter(string value)
        {
            var filter = ParseExclusionFiler(value);

            return new TimeSeriesFilter
            {
                Exclude = filter.Exclude,
                Regex = new Regex(filter.Text)
            };
        }

        private static ApprovalFilter ParseApprovalFilter(string value)
        {
            var filter = ParseExclusionFiler(value);
            var comparison = ParseComparisonFilter(filter.Text);

            return new ApprovalFilter
            {
                Exclude = filter.Exclude,
                ComparisonType = comparison.ComparisonType,
                Text = comparison.Text
            };
        }

        private static GradeFilter ParseGradeFilter(string value)
        {
            var filter = ParseExclusionFiler(value);
            var comparison = ParseComparisonFilter(filter.Text);

            return new GradeFilter
            {
                Exclude = filter.Exclude,
                ComparisonType = comparison.ComparisonType,
                Text = comparison.Text
            };
        }

        private static QualifierFilter ParseQualifierFilter(string value)
        {
            var filter = ParseExclusionFiler(value);

            return new QualifierFilter {Exclude = filter.Exclude, Text = filter.Text};
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

        private static (ComparisonType ComparisonType, string Text) ParseComparisonFilter(string value)
        {
            var comparisonType = ComparisonType.Equal;
            var text = value;

            if (value.StartsWith("<="))
            {
                comparisonType = ComparisonType.LessThanEqual;
                text = value.Substring(2);
            }
            else if(value.StartsWith("<"))
            {
                comparisonType = ComparisonType.LessThan;
                text = value.Substring(1);
            }
            else if (value.StartsWith("="))
            {
                comparisonType = ComparisonType.Equal;
                text = value.Substring(1);
            }
            else if (value.StartsWith(">="))
            {
                comparisonType = ComparisonType.GreaterThanEqual;
                text = value.Substring(2);
            }
            else if (value.StartsWith(">"))
            {
                comparisonType = ComparisonType.GreaterThan;
                text = value.Substring(1);
            }

            return (comparisonType, text);
        }
    }
}
