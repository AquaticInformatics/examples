using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using NodaTime;
using NodaTime.Text;
using ServiceStack;
using PostReflectedTimeSeries = Aquarius.TimeSeries.Client.ServiceModels.Acquisition.PostReflectedTimeSeries;
using PublishTimeSeriesPoint = Aquarius.TimeSeries.Client.ServiceModels.Publish.TimeSeriesPoint;

namespace ExternalProcessor
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
        private string _sourceTimeSeriesIdentifier;
        private string _reflectedTimeSeriesIdentifier;
        private Instant _changesSinceTime = Instant.FromDateTimeUtc(DateTime.UtcNow).Minus(Duration.FromHours(1));
        private Duration _pollInterval = Duration.FromSeconds(10);

        private void ParseArgs(string[] args)
        {
            var options = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
            {
                {"Server", value => _server = value},
                {"Username", value => _username = value},
                {"Password", value => _password = value},
                {"SourceTimeSeries", value => _sourceTimeSeriesIdentifier = value},
                {"ReflectedTimeSeries", value => _reflectedTimeSeriesIdentifier = value},
                {"ChangesSince", value => _changesSinceTime = InstantPattern.ExtendedIsoPattern.Parse(value).GetValueOrThrow()},
                {"PollInterval", value => _pollInterval = DurationPattern.RoundtripPattern.Parse(value).GetValueOrThrow()},
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

            if (string.IsNullOrWhiteSpace(_sourceTimeSeriesIdentifier))
                throw new ArgumentException($"No source time-series specified.\n\n{usageMessage}");

            if (string.IsNullOrWhiteSpace(_reflectedTimeSeriesIdentifier))
                throw new ArgumentException($"No reflected time-series specified.\n\n{usageMessage}");
        }

        private IAquariusClient _client;

        private void Run()
        {
            Console.WriteLine($"Connecting to {_server} ...");

            using (_client = AquariusClient.CreateConnectedClient(_server, _username, _password))
            {
                Console.WriteLine($"Connected to {_server} ({_client.ServerVersion})");

                PollForChangedPoints();
            }

            _client = null;
        }

        private Guid _sourceTimeSeriesUniqueId;
        private Guid _reflectedTimeSeriesUniqueId;

        private void PollForChangedPoints()
        {
            _sourceTimeSeriesUniqueId = GetTimeSeriesUniqueId(_sourceTimeSeriesIdentifier);
            _reflectedTimeSeriesUniqueId = GetTimeSeriesUniqueId(_reflectedTimeSeriesIdentifier);

            var timeSeriesType = GetTimeSeriesType(_reflectedTimeSeriesUniqueId);

            if (timeSeriesType != TimeSeriesType.Reflected)
                throw new ArgumentException($"{_reflectedTimeSeriesIdentifier} is not a reflected time-series (actual={timeSeriesType})");

            var sourceLocation = ParseLocationIdentifier(_sourceTimeSeriesIdentifier);

            while (true)
            {
                Console.WriteLine($"Polling for changes to {_sourceTimeSeriesIdentifier} since {_changesSinceTime} ...");
                var request = new TimeSeriesUniqueIdListServiceRequest
                {
                    ChangesSinceToken = _changesSinceTime.ToDateTimeUtc(),
                    LocationIdentifier = sourceLocation
                };
                var response = _client.Publish.Get(request);

                var nextToken = Instant.FromDateTimeUtc(response.NextToken ?? DateTime.UtcNow.AddHours(-1));
                _changesSinceTime = nextToken;

                var hasTokenExpired = response.TokenExpired ?? false;

                if (hasTokenExpired)
                {
                    Console.WriteLine($"The changes since token expired. Some data may have been missed. Resetting to '{response.NextToken}'");
                    continue;
                }

                var sourceTimeSeries =
                    response.TimeSeriesUniqueIds.SingleOrDefault(t => t.UniqueId == _sourceTimeSeriesUniqueId);

                if (sourceTimeSeries?.FirstPointChanged != null)
                {
                    AppendRecalculatedPoints(Instant.FromDateTimeOffset(sourceTimeSeries.FirstPointChanged.Value));
                }

                Console.WriteLine($"Sleeping for {_pollInterval} ...");
                Thread.Sleep(_pollInterval.ToTimeSpan());
            }
        }

        private void AppendRecalculatedPoints(Instant firstPointTime)
        {
            var request = new TimeSeriesDataCorrectedServiceRequest
            {
                TimeSeriesUniqueId = _sourceTimeSeriesUniqueId,
                QueryFrom = firstPointTime.ToDateTimeOffset(),
                GetParts = "PointsOnly"
            };
            var sourcePoints = _client.Publish.Get(request).Points;

            var points = RecalculatePoints(sourcePoints).ToList();

            Console.WriteLine($"Appending {points.Count} recalculated points to timeSeriesUniqueId={_reflectedTimeSeriesUniqueId:N} starting at {points.First().Time}");

            var stopwatch = Stopwatch.StartNew();

            var result = _client.Acquisition.RequestAndPollUntilComplete(
                client => client.Post(new PostReflectedTimeSeries
                {
                    UniqueId = _reflectedTimeSeriesUniqueId,
                    Points = points,
                    TimeRange = new Interval(points.First().Time.GetValueOrDefault(), Instant.MaxValue)
                }),
                (client, response) => client.Get(new GetTimeSeriesAppendStatus { AppendRequestIdentifier = response.AppendRequestIdentifier }),
                polledStatus => polledStatus.AppendStatus != AppendStatusCode.Pending);

            if (result.AppendStatus != AppendStatusCode.Completed)
                throw new Exception($"Unexpected append status={result.AppendStatus}");

            Console.WriteLine($"Appended {result.NumberOfPointsAppended} points (deleting {result.NumberOfPointsDeleted} points) in {stopwatch.ElapsedMilliseconds / 1000.0:F1} seconds.");
        }

        private IEnumerable<ReflectedTimeSeriesPoint> RecalculatePoints(IEnumerable<PublishTimeSeriesPoint> sourcePoints)
        {
            return sourcePoints.Select(p => new ReflectedTimeSeriesPoint
            {
                Time = Instant.FromDateTimeOffset(p.Timestamp.DateTimeOffset).Plus(Duration.FromSeconds(30)),
                Value = p.Value.Numeric.HasValue
                    ? p.Value.Numeric.Value * p.Value.Numeric.Value
                    : (double?)null
            });
        }

        private Guid GetTimeSeriesUniqueId(string timeSeriesIdentifier)
        {
            Guid uniqueId;

            if (Guid.TryParse(timeSeriesIdentifier, out uniqueId))
                return uniqueId;

            var location = ParseLocationIdentifier(timeSeriesIdentifier);

            var response = _client.Publish.Get(new TimeSeriesDescriptionServiceRequest {LocationIdentifier = location});

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == timeSeriesIdentifier);

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{timeSeriesIdentifier}' at location '{location}'");

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

        private TimeSeriesType GetTimeSeriesType(Guid uniqueId)
        {
            return _client.ProvisioningClient
                .Get(new GetTimeSeries {TimeSeriesUniqueId = uniqueId})
                .TimeSeriesType;
        }
    }
}
