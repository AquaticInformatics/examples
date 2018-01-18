using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace SharpShooterReportsRunner
{
    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [STAThreadAttribute]
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = 1;

                var program = new Program();
                program.ParseArgs(args);
                program.Run();

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                if (exception is ExpectedException)
                    Log.Error(exception.Message);
                else
                    Log.Error(exception.Message, exception);
            }
        }

        private static string GetProgramName()
        {
            return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
        }

        private Context Context { get; } = new Context();

        private void ParseArgs(string[] args)
        {
            var resolvedArgs = args
                .SelectMany(ResolveOptionsFromFile)
                .ToArray();

            var options = new[]
            {
                new Option {Key = nameof(Context.Server), Setter = value => Context.Server = value, Getter = () => Context.Server, Description = "AQTS app server."},
                new Option {Key = nameof(Context.Username), Setter = value => Context.Username = value, Getter = () => Context.Username, Description = "AQTS username."},
                new Option {Key = nameof(Context.Password), Setter = value => Context.Password = value, Getter = () => Context.Password, Description = "AQTS credentials."},
                new Option {Key = nameof(Context.TemplatePath), Setter = value => Context.TemplatePath = value, Getter = () => Context.TemplatePath, Description = "Path of the SharpShooter Report template file (*.RST)"},
                new Option {Key = nameof(Context.OutputPath), Setter = value => Context.OutputPath = value, Getter = () => Context.OutputPath, Description = "Path to the generated report output"},
                new Option {Key = nameof(Context.LaunchReportDesigner), Setter = value => Context.LaunchReportDesigner = bool.Parse(value), Getter = () => Context.LaunchReportDesigner.ToString(), Description = "When true, launch the SharpShooter Report Designer."},
                new Option {Key = "TimeSeries", Setter = value => Context.TimeSeries.Add(ParseTimeSeries(value)), Getter = () => string.Empty, Description = "Load the time-series as a dataset."},
                new Option {Key = "FieldVisit", Setter = value => Context.FieldVisits.Add(value), Getter = () => string.Join(", ", Context.FieldVisits), Description = "Load the location's field visits as a dataset."},
                new Option {Key = "ExternalDataSet", Setter = value => Context.ExternalDataSets.Add(ParseExternalDataSet(value)), Getter = () => string.Empty, Description = "Load the external DataSet XML file."},
                new Option {Key = nameof(Context.UploadedReportLocationIdentifier), Setter = value => Context.UploadedReportLocationIdentifier = value, Getter = () => Context.UploadedReportLocationIdentifier, Description = "Upload the generated report to this AQTS location"},
                new Option {Key = nameof(Context.UploadedReportTitle), Setter = value => Context.UploadedReportTitle = value, Getter = () => Context.UploadedReportTitle, Description = "Upload the generated report with this title"},
            };

            var usageMessage = $"Run a SharpShooter Reports template with AQTS data."
                               + $"\n\nusage: {GetProgramName()} [-option=value] [@optionsFile] ..."
                               + $"\n\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Select(o => o.UsageText()))}"
                ;

            foreach (var arg in resolvedArgs)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    if (HelpKeywords.Contains(arg))
                        throw new ExpectedException($"Showing help.\n\n{usageMessage}");

                    throw new ExpectedException($"Unknown argument '{arg}'.");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option =
                    options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

                if (option == null)
                {
                    AddReportParameter(match.Groups["key"].Value, value);
                    continue;
                }

                option.Setter(value);
            }
        }

        private void AddReportParameter(string name, string value)
        {
            if (name.Contains("."))
            {
                Context.ParameterOverrides[name] = value;
            }
            else
            {
                Context.ReportParameters[name] = value;
            }
        }

        private static readonly string[] HelpKeywords = new[]
            {
                "?", "help", "usage"
            }
            .SelectMany(s => new[] { "-" + s, "--" + s, "/" + s })
            .ToArray();

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
                .Where(s => !s.StartsWith("#"));
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);

        private static Dictionary<string, string> ParseDictionary(string value)
        {
            var components = value.Split(DictionaryEntrySeparators, StringSplitOptions.RemoveEmptyEntries);

            var dict = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                {string.Empty, components[0]}
            };

            for (var i = 1; i < components.Length; ++i)
            {
                var pair = components[i].Split(DictionaryPairSeparators, 2, StringSplitOptions.RemoveEmptyEntries);

                if (pair.Length != 2)
                    continue;

                dict.Add(pair[0], pair[1]);
            }

            return dict;
        }

        private static readonly char[] DictionaryEntrySeparators = {','};
        private static readonly char[] DictionaryPairSeparators = {'='};

        private static ExternalDataSet ParseExternalDataSet(string value)
        {
            var values = ParseDictionary(value);

            var externalDataSet = new ExternalDataSet {Path = values[string.Empty]};

            if (!File.Exists(externalDataSet.Path))
                throw new ExpectedException($"External DataSet not found at '{externalDataSet.Path}'.");

            if (values.ContainsKey("Name"))
                externalDataSet.Name = values["Name"];

            return externalDataSet;
        }

        private static TimeSeries ParseTimeSeries(string value)
        {
            var values = ParseDictionary(value);

            return new TimeSeries
            {
                Identifier = values[string.Empty],
                OutputUnitId = GetValueOrDefault(values, "Unit"),
                QueryFrom = GetValueOrDefault(values, "From"),
                QueryTo = GetValueOrDefault(values, "To"),
                GroupBy = GetValueOrDefault(values, "GroupBy", ReportRunner.GroupBy.Year.ToString()),
            };
        }

        private static string GetValueOrDefault(Dictionary<string, string> values, string key, string defaultValue = null)
        {
            return values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private void Run()
        {
            using (var reportRunner = new ReportRunner(Context))
            {
                reportRunner.Run();
            }
        }
    }
}
