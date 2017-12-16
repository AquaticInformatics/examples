using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Results;
using log4net;
using ServiceStack;
using ServiceStack.Text;
using ILog = FieldDataPluginFramework.ILog;

namespace PluginTester
{
    public class Program
    {
        private static readonly log4net.ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            try
            {
                ConfigureJson();

                var program = new Program();
                program.ParseArgs(args);
                program.Run();

                Environment.ExitCode = 0;
            }
            catch (ExpectedException exception)
            {
                Log.Error(exception.Message);
            }
            catch (Exception exception)
            {
                Log.Error("Unhandled exception", exception);
            }
        }

        private static void ConfigureJson()
        {
            JsConfig.ExcludeTypeInfo = true;
            JsConfig.DateHandler = DateHandler.ISO8601DateTime;
            JsConfig.IncludeNullValues = true;
            JsConfig.IncludeNullValuesInDictionaries = true;

            JsConfig<DateTimeOffset>.SerializeFn = offset => offset.ToString("O");
            JsConfig<DateTimeOffset?>.SerializeFn = offset => offset?.ToString("O") ?? string.Empty;
        }

        private static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        }

        private string PluginPath { get; set; }
        private string DataPath { get; set; }
        private string LocationIdentifier { get; set; }
        private string JsonPath { get; set; }

        private void ParseArgs(string[] args)
        {
            var options = new[]
            {
                new Option {Key = "Plugin", Setter = value => PluginPath = value, Getter = () => PluginPath, Description = "Path to the plugin assembly to debug"},
                new Option {Key = "Data", Setter = value => DataPath = value, Getter = () => DataPath, Description = "Path to the data file to be parsed"},
                new Option {Key = "Location", Setter = value => LocationIdentifier = value, Getter = () => LocationIdentifier, Description = "Optional location identifier context"},
                new Option {Key = "Json", Setter = value => JsonPath = value, Getter = () => JsonPath, Description = "Optional path to write the appended results as JSON"},
            };

            var usageMessage = $"Parse a file using a field data plugin, logging the results."
                                   + $"\n\nusage: {GetProgramName()} [-option=value] ..."
                                   + $"\n\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                                   ;

            foreach (var arg in args)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
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

            if (string.IsNullOrEmpty(PluginPath))
                throw new ExpectedException("No plugin assembly specified.");

            if (string.IsNullOrEmpty(DataPath))
                throw new ExpectedException("No data file specified.");
        }

        private class Option
        {
            public string Key { get; set; }
            public string Description { get; set; }
            public Action<string> Setter { get; set; }
            public Func<string> Getter { get; set; }

            public string UsageText()
            {
                var defaultValue = Getter();

                if (!string.IsNullOrEmpty(defaultValue))
                    defaultValue = $" [default: {defaultValue}]";

                return $"{Key,-20} {Description}{defaultValue}";
            }
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private void Run()
        {
            using (var stream = LoadDataStream())
            {
                var locationInfo = !string.IsNullOrEmpty(LocationIdentifier)
                    ? FieldDataResultsAppender.CreateLocationInfo(LocationIdentifier)
                    : null;

                var plugin = LoadPlugin();
                var logger = CreateLogger();
                var appender = new FieldDataResultsAppender {LocationInfo = locationInfo};

                try
                {
                    appender.AppendedResults.PluginAssemblyQualifiedTypeName = plugin.GetType().AssemblyQualifiedName;

                    var result = string.IsNullOrEmpty(LocationIdentifier)
                        ? plugin.ParseFile(stream, appender, logger)
                        : plugin.ParseFile(stream, locationInfo, appender, logger);


                    SaveAppendedResults(appender.AppendedResults);

                    SummarizeResults(result, appender.AppendedResults);
                }
                catch (Exception exception)
                {
                    Log.Error("Plugin has thrown an error", exception);

                    throw new ExpectedException($"Unhandled plugin exception: {exception.Message}");
                }
            }
        }

        private void SaveAppendedResults(AppendedResults appendedResults)
        {
            if (string.IsNullOrEmpty(JsonPath))
                return;

            Log.Info($"Saving {appendedResults.AppendedVisits.Count} visits data to '{JsonPath}'");

            File.WriteAllText(JsonPath, appendedResults.ToJson().IndentJson());
        }

        private void SummarizeResults(ParseFileResult result, AppendedResults appendedResults)
        {
            if (!result.Parsed)
            {
                if (result.Status == ParseFileStatus.CannotParse)
                {
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        Log.Error($"Can't parse '{DataPath}'. {result.ErrorMessage}");
                    }
                    else
                    {
                        Log.Warn($"File '{DataPath}' is not parsed by the plugin.");
                    }
                }
                else
                {
                    Log.Error($"Result: Parsed={result.Parsed} Status={result.Status} ErrorMessage={result.ErrorMessage}");
                }
            }
            else
            {
                if (!appendedResults.AppendedVisits.Any())
                {
                    Log.Warn("File was parsed but no visits were appended.");
                }
                else
                {
                    Log.Info($"Successfully parsed {appendedResults.AppendedVisits.Count} visits.");
                }
            }
        }

        private Stream LoadDataStream()
        {
            if (!File.Exists(DataPath))
                throw new ExpectedException($"Data file '{DataPath}' does not exist.");

            Log.Info($"Loading data file '{DataPath}'");

            using (var stream = new FileStream(DataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(var reader = new BinaryReader(stream))
            {
                return new MemoryStream(reader.ReadBytes((int)stream.Length));
            }
        }

        private IFieldDataPlugin LoadPlugin()
        {
            var pluginPath = Path.GetFullPath(PluginPath);

            if (!File.Exists(pluginPath))
                throw new ExpectedException($"Plugin file '{pluginPath}' does not exist.");

            // ReSharper disable once PossibleNullReferenceException
            var assembliesInPluginFolder = new FileInfo(pluginPath).Directory.GetFiles("*.dll");

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var dll = assembliesInPluginFolder.FirstOrDefault(fi =>
                    args.Name.StartsWith(Path.GetFileNameWithoutExtension(fi.Name) + ", ",
                        StringComparison.InvariantCultureIgnoreCase));

                return dll == null ? null : Assembly.LoadFrom(dll.FullName);
            };

            var assembly = Assembly.LoadFile(pluginPath);

            var pluginTypes = (
                    from type in assembly.GetTypes()
                    where typeof(IFieldDataPlugin).IsAssignableFrom(type)
                    select type
                ).ToList();

            if (pluginTypes.Count == 0)
                throw new ExpectedException($"No IFieldDataPlugin plugin implementations found in '{pluginPath}'.");

            if (pluginTypes.Count > 1)
                throw new ExpectedException($"{pluginTypes.Count} IFieldDataPlugin plugin implementations found in '{pluginPath}'.");

            var pluginType = pluginTypes.Single();

            return Activator.CreateInstance(pluginType) as IFieldDataPlugin;
        }

        private ILog CreateLogger()
        {
            return Log4NetLogger.Create(LogManager.GetLogger(Path.GetFileNameWithoutExtension(PluginPath)));
        }
    }
}
