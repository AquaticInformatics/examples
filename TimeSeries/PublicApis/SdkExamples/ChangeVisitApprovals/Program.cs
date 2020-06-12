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

namespace ChangeVisitApprovals
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
            catch (WebServiceException exception)
            {
                Log.Error($"API: ({exception.StatusCode}) {string.Join(" ", exception.StatusDescription, exception.ErrorCode)}: {string.Join(" ", exception.Message, exception.ErrorMessage)}", exception);
            }
            catch (ExpectedException exception)
            {
                Log.Error(exception.Message);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
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
                new Option {Key = nameof(context.SessionToken), Setter = value => context.SessionToken = value, Getter = () => context.SessionToken},
                new Option {Key = nameof(context.ApprovalLevel), Setter = value => context.ApprovalLevel = int.Parse(value), Getter = () => context.ApprovalLevel.ToString(), Description = "Sets the target approval level by numeric value."},
                new Option {Key = nameof(context.ApprovalName), Setter = value => context.ApprovalName = value, Getter = () => context.ApprovalName, Description = "Sets the target approval level by name."},
                new Option {Key = nameof(context.SkipConfirmation), Setter = value => context.SkipConfirmation = bool.Parse(value), Getter = () => context.SkipConfirmation.ToString(), Description = "When true, skip the confirmation prompt. '/Y' is a shortcut for this option."},
                new Option {Key = nameof(context.DryRun), Setter = value => context.DryRun = bool.Parse(value), Getter = () => context.DryRun.ToString(), Description = "When true, don't make any changes. '/N' is a shortcut for this option."},
                new Option {Key = "Location", Setter = value => context.Locations.Add(value), Getter = () => string.Join(", ", context.Locations), Description = "Locations to examine."},
                new Option {Key = nameof(context.VisitsBefore), Setter = value => context.VisitsBefore = ParseDateTime(value), Getter = () => context.VisitsBefore?.ToString("O"), Description = "Change all visits in matching locations before and including this date."},
                new Option {Key = nameof(context.VisitsAfter), Setter = value => context.VisitsAfter = ParseDateTime(value), Getter = () => context.VisitsAfter?.ToString("O"), Description = "Change all visits in matching locations after and including this date."},
            };

            var usageMessage
                    = $"Changes field visit approval levels on an AQTS server."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] [location] ..."
                      + $"\n"
                      + $"\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                      + $"\n"
                      + $"\nUse the @optionsFile syntax to read more options from a file."
                      + $"\n"
                      + $"\n  Each line in the file is treated as a command line option."
                      + $"\n  Blank lines and leading/trailing whitespace is ignored."
                      + $"\n  Comment lines begin with a # or // marker."
                      + $"\n"
                      + $"\nLocation filtering:"
                      + $"\n================="
                      + $"\nLocations can be specified by either location identifier or location unique ID."
                      + $"\nLocation identifiers are matched case-insensitively."
                      + $"\nPublish API wildcard expansion of '*' is supported. '02*' will match locations beginning with '02'."
                      + $"\n"
                      + $"\nTime-range filtering:"
                      + $"\n====================="
                      + $"\nWhen the /{nameof(context.VisitsBefore)}= or /{nameof(context.VisitsAfter)}= options are given, only the visits falling within the time-range will be changed."
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

                    // Otherwise assume it is a location constraint
                    context.Locations.Add(arg);
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

            if (string.IsNullOrWhiteSpace(context.SessionToken) && (string.IsNullOrWhiteSpace(context.Username) || string.IsNullOrWhiteSpace(context.Password)))
                throw new ExpectedException("Valid AQTS credentials must be supplied.");

            if (!context.ApprovalLevel.HasValue && string.IsNullOrWhiteSpace(context.ApprovalName))
                throw new ExpectedException($"You must specify a target approval level with /{nameof(context.ApprovalName)} or /{nameof(context.ApprovalLevel)} options.");

            if (context.ApprovalLevel.HasValue && !string.IsNullOrWhiteSpace(context.ApprovalName))
                throw new ExpectedException($"Only one of /{nameof(context.ApprovalName)} or /{nameof(context.ApprovalLevel)} can be specified.");

            return context;
        }

        private static string GetProgramName()
        {
            // ReSharper disable once PossibleNullReferenceException
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
            new ApprovalChanger {Context = _context}
                .Run();
        }
    }
}
