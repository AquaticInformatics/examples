using Mono.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReflectedSeriesAggregator
{
    public enum ReadTimeSeriesFromType
    {
        SourceSOR,
        TargetEOR,
        TimeStamp
    };

    public class Settings
    {
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string AggrSeriesLabelSingle { get; set; }
        public string AggrSeriesLabelMulti { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public List<string> ParameterIds { get; set; } = new List<string>();
        public bool AdjustDuplicateTimestamps { get; set; }
        public bool ShouldShowHelp { get; set; } = false;
        public bool Publish { get; set; } = false;
        public string ReadTimeSeriesFrom { get; set; } = string.Empty;
        public bool Aggregate { get; set; }
    }

    public static class ArgHandler
    {
        static OptionSet GetOptionSet(Settings settings)
        {
            return new OptionSet {
                { "SetPassword=", "Set a new password for API access. Replaces -Password settings in @files. Can only be used in a command line argument and not an @file.", p => { /*dummy placeholder */} },
                { "Server=", "AQ TS host name or ip address.", h => settings.Server = h },
                { "Username=", "AQ TS username for API access.", u => settings.Username = u },
                { "Password=", "AQ TS password for API access.", p => {
                    try
                    {
                        settings.Password = Encryption.DecryptCipherTextToPlainText(p);
                    }
                    catch(Exception ex)
                    {
                        throw new Exception("An encrypted password is required. Please provide -SetPassword in the command line (and not the file)", ex);
                    }}
                },
                { "Tags=", "List of tags which describe the tags to search for.",
                    t => {
                        if( string.IsNullOrWhiteSpace(t))
                            throw new Exception("Tags= setting must be set with a value");

                        if( settings.Tags.Contains(t))
                            throw new Exception($"Tags {t} already specified");
                        settings.Tags.Add(t);
                    }
                },
                { "AggrSeriesLabel_Single=", "The aggregation label to use for every aggregated series. Defaults to \"Aggregated\".", l => settings.AggrSeriesLabelSingle = l },
                { "AggrSeriesLabel_Multi=", "The aggregation label to use for every aggregated series. Defaults to \"Aggregated\".", l => settings.AggrSeriesLabelMulti = l },
                { "Labels=", "Filter by label. If no -Labels= options are set, all AQSamples reflected series will be aggregated.",
                    l => {
                       if( string.IsNullOrWhiteSpace(l))
                            throw new Exception("Label= setting must be set with a value.");

                        if( settings.Labels.Contains(l))
                            throw new Exception($"Label {l} already specified");
                        settings.Labels.Add(l);
                        }
                },
                { "ParameterIds=", "Filter by parameterID. If no -ParameterIds= options are set, all series will be aggregated.", t => settings.ParameterIds.Add(t) },
                { "Publish=", "Set publish flag on newly created time series.  The locations publish must also be set to true", p => settings.Publish = bool.Parse(p) },
                { "ReadTimeSeriesFrom=", "AQ TS password for API access.", r => settings.ReadTimeSeriesFrom = r },
                { "AdjustDuplicateTimestamps=", "How to handle duplicate timestamps in the aggregated points.   When duplicate point timestamps are encountered, always log an ERROR message showing the timestamp, source series identifiers, and source values. Defaults to false. If false, don't add any point values at that ambiguous timestamp.If true, add a second to each of the duplicate points to make them distinct.", a => settings.AdjustDuplicateTimestamps = bool.Parse(a) },
                { "Aggregate=", "If set to true data aggregation is performed. Otherwise false to display the list of aggregates.", d=> settings.Aggregate = bool.Parse(d) },
                { "?|help", "show this message and exit", h => settings.ShouldShowHelp = h != null }
            };
        }

        static List<string> ExpandArguments(string[] args)
        {
            var expandedArgs = new List<string>();
            foreach (var arg in args)
            {
                if (!arg.StartsWith("@"))
                {
                    expandedArgs.Add(arg);
                    continue;
                }

                var filePath = arg.TrimStart('@');
                var fileArgs =
                    System.IO.File.ReadAllLines(filePath)
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("#"))
                    .Select(l => l.Trim()).ToArray();

                if (fileArgs.Any(a => a.StartsWith("-NewPassword=")))
                    throw new Exception($"SetPassword must be set as a command line argument.  Please remove from file {filePath}.");

                expandedArgs.AddRange(fileArgs);
            }

            return expandedArgs;
        }

        public static bool Parse(string[] args, ILogger logger, out Settings settings)
        {
            settings = new Settings();
            var options = GetOptionSet(settings);

            List<string> extra;
            try
            {
                if (SetNewPassword(args, logger))
                    return false;

                var expandedArgs = ExpandArguments(args);
                extra = options.Parse(expandedArgs.ToArray());

                if (settings.ShouldShowHelp)
                {
                    Console.WriteLine("Options:");
                    options.WriteOptionDescriptions(Console.Out);
                    return false;
                }

                Validate(settings);
                return true;
            }
            catch (Exception e)
            {
                // output some error message
                Console.WriteLine("Command line argument error:");
                Console.WriteLine(e.Message);
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return false;
            }
        }

        private static bool SetNewPassword(string[] args, ILogger logger)
        {
            string newPassword = args.Where(arg => arg.Trim().StartsWith("-SetPassword=")).FirstOrDefault()?.Split("=".ToCharArray())[1];
            if (string.IsNullOrWhiteSpace(newPassword))
                return false;

            string replacementSetting = $"-Password={Encryption.EncryptPlainTextToCipherText(newPassword)}";

            var files = args.Where(arg => arg.StartsWith("@")).Select(arg => arg.TrimStart('@')).ToList();
            if (files.Count == 0)
                throw new Exception("No setting files specified to change");

            int changesMade = 0;
            foreach (var file in files)
            {
                var fileLines = System.IO.File.ReadAllLines(file).ToArray();

                bool found = false;
                for (int idx = 0; idx < fileLines.Length; idx++)
                {
                    if (fileLines[idx].Trim().StartsWith("-Password="))
                    {
                        changesMade++;
                        found = true;
                        fileLines[idx] = replacementSetting;
                    }
                }

                if (found)
                    System.IO.File.WriteAllLines(file, fileLines);
            }

            if (changesMade == 0)
            {
                var file = files.First();
                var fileLines = System.IO.File.ReadAllLines(file).ToList();
                fileLines.Insert(0, replacementSetting);
                System.IO.File.WriteAllLines(file, fileLines);
            }

            logger.Information("New password set");
            return true;
        }

        static void Validate(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Server))
                throw new Exception("Server: An AQ TS Server name or ip address is required.");
            if (string.IsNullOrWhiteSpace(settings.Username))
                throw new Exception("Username: An AQ TS username for API access is required.");
            if (string.IsNullOrWhiteSpace(settings.Password))
                throw new Exception("Password: An AQ TS password for API access is required.");
            if (string.IsNullOrWhiteSpace(settings.AggrSeriesLabelSingle))
                throw new Exception("AggregatedSeriesLabelForSingleLabel: An aggregate label must be set for time-series creation.");
            if (string.IsNullOrWhiteSpace(settings.AggrSeriesLabelMulti))
                throw new Exception("AggregatedSeriesLabelForMultiLabels: An aggregate label must be set for time-series creation.");
        }
    }
}
