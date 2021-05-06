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

        [STAThread] // Single-threaded COM compatibility is required by SharpShooter Reports
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
            // ReSharper disable once PossibleNullReferenceException
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
                new Option {Description = "AQUARIUS Time-Series connection options:"},
                new Option
                {
                    Key = nameof(Context.Server),
                    Setter = value => Context.Server = value,
                    Getter = () => Context.Server
                    , Description = "The AQTS app server from which time-series data will be retrieved."
                },
                new Option
                {
                    Key = nameof(Context.Username),
                    Setter = value => Context.Username = value,
                    Getter = () => Context.Username,
                    Description = "AQTS username."
                },
                new Option
                {
                    Key = nameof(Context.Password),
                    Setter = value => Context.Password = value,
                    Getter = () => Context.Password,
                    Description = "AQTS credentials."
                },

                new Option(), new Option {Description = "SharpShooter Reports options:"},
                new Option
                {
                    Key = nameof(Context.TemplatePath),
                    Setter = value => Context.TemplatePath = value,
                    Getter = () => Context.TemplatePath,
                    Description = "Path of the SharpShooter Reports template file (*.RST)"
                },
                new Option
                {
                    Key = nameof(Context.OutputPath),
                    Setter = value => Context.OutputPath = value,
                    Getter = () => Context.OutputPath,
                    Description = "Path to the generated report output. Only PDF output is supported."
                },
                new Option
                {
                    Key = nameof(Context.LaunchReportDesigner),
                    Setter = value => Context.LaunchReportDesigner = bool.Parse(value),
                    Getter = () => Context.LaunchReportDesigner.ToString(),
                    Description = "When true, launch the SharpShooter Report Designer."
                },
                
                new Option(), new Option {Description = "Dataset options:"},
                new Option
                {
                    Key = nameof(Context.QueryFrom),
                    Setter = value => Context.QueryFrom = value,
                    Getter = () => Context.QueryFrom,
                    Description = "The starting point for all time-series. Can be overriden by individual series. [default: Beginning of record]"
                },
                new Option
                {
                    Key = nameof(Context.QueryTo),
                    Setter = value => Context.QueryTo = value,
                    Getter = () => Context.QueryTo,
                    Description = "The ending point for all time-series. Can be overriden by individual series. [default: End of record]"
                },
                new Option
                {
                    Key = nameof(Context.GroupBy),
                    Setter = value => Context.GroupBy = ParseEnum<GroupBy>(value),
                    Getter = () => $"{Context.GroupBy}",
                    Description = $"The grouping for all time-series. One of {string.Join(", ", Enum.GetNames(typeof(GroupBy)))}. Can be overriden by individual series."
                },
                new Option
                {
                    Key = "TimeSeries",
                    Setter = value => Context.TimeSeries.Add(ParseTimeSeries(value)),
                    Getter = () => string.Empty,
                    Description = "Load the specified time-series as a dataset."
                },
                new Option
                {
                    Key = "RatingModel",
                    Setter = value => Context.RatingModels.Add(ParseRatingModel(value)),
                    Getter = () => string.Empty,
                    Description = "Load the specified rating-model as a dataset."
                },
                new Option
                {
                    Key = "ExternalDataSet",
                    Setter = value => Context.ExternalDataSets.Add(ParseExternalDataSet(value)),
                    Getter = () => string.Empty,
                    Description = "Load the external DataSet XML file."
                },
                
                new Option(), new Option {Description = "Report uploading options:"},
                new Option
                {
                    Key = nameof(Context.UploadedReportLocation),
                    Setter = value => Context.UploadedReportLocation = value,
                    Getter = () => Context.UploadedReportLocation,
                    Description = "Upload the generated report to this AQTS location identifier. If empty, no report will be uploaded."
                },
                new Option
                {
                    Key = nameof(Context.UploadedReportTitle),
                    Setter = value => Context.UploadedReportTitle = value,
                    Getter = () => Context.UploadedReportTitle,
                    Description = $"Upload the generated report with this title. Defaults to the -{nameof(Context.OutputPath)} base filename."
                },
            };

            var usageMessage
                = $"Run a SharpShooter Reports template with AQTS data."
                + $"\n"
                + $"\nusage: {GetProgramName()} [-option=value] [@optionsFile] ..."
                + $"\n"
                + $"\nSupported -option=value settings (/option=value works too):\n\n  {string.Join("\n  ", options.Select(o => o.UsageText()))}"
                + $"\n"
                + $"\nRetrieving time-series data from AQTS: (more than one -TimeSeries=value option can be specified)"
                + $"\n"
                + $"\n  -TimeSeries=identifierOrUniqueId[,From=date][,To=date][,Unit=outputUnit][,GroupBy=option]"
                + $"\n"
                + $"\n     =identifierOrUniqueId - Use either the uniqueId or the <parameter>.<label>@<location> syntax."
                + $"\n     ,From=date            - Retrieve data from this date. [default: Beginning of record]"
                + $"\n     ,To=date              - Retrieve data until this date. [default: End of record]"
                + $"\n     ,Unit=outputUnit      - Convert the values to the unit. [default: The default unit of the time-series]"
                + $"\n     ,GroupBy=option       - Groups data by {string.Join("|", Enum.GetNames(typeof(GroupBy)))} [default: {Context.GroupBy}]"
                + $"\n"
                + $"\n  Dates specified as yyyy-MM-ddThh:mm:ss.fff. Only the year component is required."
                + $"\n"
                + $"\nRetrieving rating model info from AQTS: (more than one -RatingModel=value option can be specified)"
                + $"\n"
                + $"\n  -RatingModel=identifierOrUniqueId[,From=date][,To=date][,Unit=outputUnit][,GroupBy=option]"
                + $"\n"
                + $"\n     =identifierOrUniqueId - Use either the uniqueId or the <InputParameter>-<OutputParameter>.<label>@<location> syntax."
                + $"\n     ,From=date            - Retrieve data from this date. [default: Beginning of record]"
                + $"\n     ,To=date              - Retrieve data until this date. [default: End of record]"
                + $"\n     ,StepSize=increment   - Set the expanded table step size. [default: 0.1]"
                + $"\n"
                + $"\n  Dates specified as yyyy-MM-ddThh:mm:ss.fff. Only the year component is required."
                + $"\n"
                + $"\nUsing external data sets: (more than one -ExternalDataSet=value option can be specified)"
                + $"\n"
                + $"\n  -ExternalDataSet=pathToXml[,Name=datasetName]"
                + $"\n"
                + $"\n     =pathToXml            - A standard .NET DataSet, serialized to XML."
                + $"\n     ,Name=datasetName     - Override the name of the dataset. [default: The name stored within the XML]"
                + $"\n"
                + $"\nUnknown -name=value options will be merged with the appropriate data set and table."
                + $"\n"
                + $"\n  Simple -name=value options like -MySetting=MyValue will be added to the Common.CommandLineParameters table."
                + $"\n"
                + $"\n  Dotted -name=value options like -ReportParameters.Parameters.Description=MyValue will be merged into the named dataset.table.column."
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
                        throw new ExpectedException($"Showing help.\n\n{usageMessage}");

                    throw new ExpectedException($"Unknown argument '{arg}'.");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                var option = options.FirstOrDefault(o => o.Key != null && o.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

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
                .Where(s => !s.StartsWith("#") && !s.StartsWith("//"));
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

            var groupByText = GetValueOrDefault(values, "GroupBy", GroupBy.Year.ToString());

            return new TimeSeries
            {
                Identifier = values[string.Empty],
                OutputUnitId = GetValueOrDefault(values, "Unit"),
                QueryFrom = GetValueOrDefault(values, "From"),
                QueryTo = GetValueOrDefault(values, "To"),
                GroupBy = ParseEnum<GroupBy>(groupByText),
            };
        }

        private static TEnum ParseEnum<TEnum>(string text) where TEnum : struct
        {
            if (Enum.TryParse<TEnum>(text, true, out var enumValue))
                return enumValue;

            throw new ExpectedException($"'{text}' is not a supported {typeof(TEnum).Name} value. Must be one of {string.Join(", ", Enum.GetNames(typeof(TEnum)))}");
        }

        private static RatingModel ParseRatingModel(string value)
        {
            var values = ParseDictionary(value);

            return new RatingModel
            {
                Identifier = values[string.Empty],
                QueryFrom = GetValueOrDefault(values, "From"),
                QueryTo = GetValueOrDefault(values, "To"),
                StepSize = GetValueOrDefault(values, "StepSize"),
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
