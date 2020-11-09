using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;

namespace NEM12PreProcessor
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

                new Program()
                    .Run(context);

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                Action<string> logAction = message => _log.Error(message);

                void ExceptionAction(Exception ex)
                {
                    if (ex is ExpectedException)
                        logAction(ex.Message);
                    else
                        logAction($"{ex.Message}\n{ex.StackTrace}");
                }

                if (_log == null)
                {
                    logAction = message => Console.WriteLine($"FATAL ERROR (logging not configured): {message}");
                }

                if (exception is AggregateException aggregateException)
                {
                    foreach (var innerException in aggregateException.InnerExceptions)
                    {
                        ExceptionAction(innerException);
                    }
                }
                else
                {
                    ExceptionAction(exception);
                }
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
            }
        }

        public static string GetProgramName()
        {
            // ReSharper disable once PossibleNullReferenceException
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
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
                new Option
                {
                    Key = nameof(context.Files),
                    Description = "Parse the NEM12 file. Can be set multiple times.",
                    Setter = value => context.Files.Add(value),
                    Getter = () => string.Empty,
                },
            };

            var usageMessage
                    = $"Converts the NEM12 CSV file into a single CSV row per point."
                      + $"\n"
                      + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] NEM12File1 NEM12File2 ..."
                      + $"\n"
                      + $"\nIf no NEM12 CSV file is specified, the standard input will be used."
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

                    if (File.Exists(arg))
                    {
                        context.Files.Add(arg);
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

        private const string StandardInputToken = "-";

        private void Run(Context context)
        {
            if (!context.Files.Any())
            {
                context.Files.Add(StandardInputToken);
            }

            foreach (var path in context.Files)
            {
                ParseFile(path);
            }
        }

        private void ParseFile(string path)
        {
            if (path == StandardInputToken)
            {
                ParseStream(Console.OpenStandardInput());
                return;
            }

            if (!File.Exists(path))
                throw new ExpectedException($"'{path}' is not a valid file.");

            using (var stream = File.OpenRead(path))
            {
                ParseStream(stream);
            }
        }

        private void ParseStream(Stream stream)
        {
            new Parser()
                .ProcessStream(stream);
        }
    }
}
