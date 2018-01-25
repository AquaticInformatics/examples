using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using NodaTime;
using NodaTime.Text;
using ServiceStack;
using TimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.TimeSeriesPoint;

namespace AppendPoints
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Environment.ExitCode = 1;

            try
            {
                var program = new Program();

                program.ParseArgs(args);
                program.Run();

                Environment.ExitCode = 0;
            }
            catch (WebServiceException exception)
            {
                Console.WriteLine($"ERROR: {exception.ErrorCode} {exception.ErrorMessage}\n\n{exception.StackTrace}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"ERROR: {exception.Message}\n\n{exception.StackTrace}");
            }
        }

        private static readonly Regex ArgRegex = new Regex(@"^([/-])(?<key>[^=]+)=(?<value>.*)$");

        private string _server = "localhost";
        private string _username = "admin";
        private string _password = "admin";
        private string _timeSeriesIdentifier;
        private Instant _startTime = Instant.FromDateTimeUtc(DateTime.UtcNow);
        private double _startValue = 1;
        private int _numberOfPoints = 1;
        private Duration _timeIncrement = Duration.FromHours(1);
        private double _valueIncrement = 1;

        private void ParseArgs(string[] args)
        {
            var options = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
            {
                {"Server", value => _server = value},
                {"Username", value => _username = value},
                {"Password", value => _password = value},
                {"TimeSeries", value => _timeSeriesIdentifier = value},
                {"StartTime", value => _startTime = InstantPattern.ExtendedIsoPattern.Parse(value).GetValueOrThrow()},
                {"StartValue", value => _startValue = double.Parse(value)},
                {"NumberOfPoints", value => _numberOfPoints = int.Parse(value)},
                {"TimeIncrement", value => _timeIncrement = DurationPattern.RoundtripPattern.Parse(value).GetValueOrThrow()},
                {"ValueIncrement", value => _valueIncrement = double.Parse(value)},
            };

            var programName =
                System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            var usageMessage =
                $"usage: {programName} [-option=value] ...\n\nSupported -option=value settings (/option=value works too):\n\n  -{string.Join("\n  -", options.Keys)}";

            foreach (var arg in args)
            {
                var match = ArgRegex.Match(arg);

                if (!match.Success)
                {
                    throw new ArgumentException($"Unknown value {arg}\n\n{usageMessage}");
                }

                var key = match.Groups["key"].Value.ToLower();
                var value = match.Groups["value"].Value;

                Action<string> setterAction;

                if (!options.TryGetValue(key, out setterAction))
                {
                    throw new ArgumentException($"Unknown -option=value: {arg}\n\n{usageMessage}");
                }

                setterAction(value);
            }

            if (string.IsNullOrWhiteSpace(_timeSeriesIdentifier))
                throw new ArgumentException($"No time-series specified.\n\n{usageMessage}");
        }

        private IAquariusClient _client;

        private void Run()
        {
            Console.WriteLine($"Connecting to {_server} ...");

            using (_client = AquariusClient.CreateConnectedClient(_server, _username, _password))
            {
                Console.WriteLine($"Connected to {_server} ({_client.ServerVersion})");

                AppendPoints();
            }

            _client = null;
        }

        private void AppendPoints()
        {
            var uniqueId = GetTimeSeriesUniqueId();
            var points = CreatePoints().ToList();

            Console.WriteLine($"Appending {points.Count} to timeSeriesUniqueId={uniqueId:N} starting at {points.First().Time}");

            var stopwatch = Stopwatch.StartNew();

            var result = _client.Acquisition.RequestAndPollUntilComplete(
                client => client.Post(new PostTimeSeriesAppend {UniqueId = uniqueId, Points = points}),
                (client, response) => client.Get(new GetTimeSeriesAppendStatus {AppendRequestIdentifier = response.AppendRequestIdentifier}),
                polledStatus => polledStatus.AppendStatus != AppendStatusCode.Pending);

            if (result.AppendStatus != AppendStatusCode.Completed)
                throw new Exception($"Unexpected append status={result.AppendStatus}");

            Console.WriteLine($"Appended {result.NumberOfPointsAppended} points (deleting {result.NumberOfPointsDeleted} points) in {stopwatch.ElapsedMilliseconds/1000.0:F1} seconds.");
        }

        private Guid GetTimeSeriesUniqueId()
        {
            Guid uniqueId;

            if (Guid.TryParse(_timeSeriesIdentifier, out uniqueId))
                return uniqueId;

            var location = ParseLocationIdentifier(_timeSeriesIdentifier);

            var response = _client.Publish.Get(new TimeSeriesDescriptionServiceRequest {LocationIdentifier = location});

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == _timeSeriesIdentifier);

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{_timeSeriesIdentifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

        private static string ParseLocationIdentifier(string timeSeriesIdentifier)
        {
            var match = IdentifierRegex.Match(timeSeriesIdentifier);

            if (!match.Success)
                throw new ArgumentException($"Can't parse '{timeSeriesIdentifier}' as time-series identifier. Expecting <Parameter>.<Label>@<Location>");

            return match.Groups["location"].Value;
        }

        private static readonly Regex IdentifierRegex = new Regex(@"^(?<parameter>[^.]+)\.(?<label>[^@]+)@(?<location>.*)$");

        private IEnumerable<TimeSeriesPoint> CreatePoints()
        {
            if (_numberOfPoints < 1)
                throw new ArgumentOutOfRangeException($"1 or more points must be appended.");

            var value = _startValue;
            var time = _startTime;

            for (var i = 0; i < _numberOfPoints; ++i)
            {
                yield return new TimeSeriesPoint
                {
                    Time = time,
                    Value = value
                };

                time = time.Plus(_timeIncrement);
                value += _valueIncrement;
            }
        }
    }
}
