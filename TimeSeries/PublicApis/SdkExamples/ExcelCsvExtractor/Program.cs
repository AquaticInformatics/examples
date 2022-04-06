using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using log4net;

namespace ExcelCsvExtractor
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

                var context = GetContext(args);
                new Program(context)
                    .Run();

                Environment.ExitCode = 0;
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

                using (var reader = new BinaryReader(stream))
                {
                    return reader.ReadBytes((int)stream.Length);
                }
            }
        }

        private static Context GetContext(string[] args)
        {
            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            var context = new Context();

            var options = new[]
            {
                new Option
                {
                    Key = nameof(context.ExcelPath),
                    Setter = value => context.ExcelPath = value,
                    Getter = () => context.ExcelPath,
                    Description = "Specifies the Excel workbook to split"
                },
                new Option
                {
                    Key = nameof(context.Sheets),
                    Setter = value => context.Sheets.Add(value),
                    Description = "Split out the named sheets. [default: All sheets]"
                },
                new Option
                {
                    Key = nameof(context.OutputPath),
                    Setter = value => context.OutputPath = value,
                    Getter = ()=> context.OutputPath,
                    Description = $"Output path of CSVs (default: {{{Splitter.ExcelPathPattern}}}.{{{Splitter.SheetNamePattern}}}.csv"
                },
                new Option
                {
                    Key = nameof(context.Overwrite),
                    Setter = value => context.Overwrite = bool.Parse(value),
                    Getter = () => $"{context.Overwrite}",
                    Description = "Set to true to overwrite existing files."
                },
                new Option
                {
                    Key = nameof(context.TrimEmptyColumns),
                    Setter = value => context.TrimEmptyColumns = bool.Parse(value),
                    Getter = () => $"{context.TrimEmptyColumns}",
                    Description = "Set to false to retain empty columns at the end of each row."
                },
                new Option
                {
                    Key = nameof(context.DateTimeFormat),
                    Setter = value => context.DateTimeFormat = value,
                    Getter = () => context.DateTimeFormat,
                    Description = "Sets the format of any timestamps, using .NET datetime format [default: ISO8601]"
                },
            };

            var usageMessage
                    = $"Extracts all the sheets in an Excel workbook into multiple CSV files"
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
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (ResolvePositionalArgument(context, arg))
                        continue;

                    throw new ExpectedException($"Unknown argument: {arg}\n\n{usageMessage}");
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

            ValidateContext(context);

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


        private static void ValidateContext(Context context)
        {
            if (!File.Exists(context.ExcelPath))
                throw new ExpectedException($"The /{nameof(context.ExcelPath)} argument is required.");
        }

        private static bool ResolvePositionalArgument(Context context, string arg)
        {
            if (File.Exists(arg))
            {
                context.ExcelPath = arg;
                return true;
            }

            return false;
        }

        private readonly Context _context;

        public Program(Context context)
        {
            _context = context;
        }

        private void Run()
        {
            new Splitter
                {
                    Context = _context
                }
                .Run();
        }
    }
}
