using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using Get3xCorrectedData = Aquarius.TimeSeries.Client.ServiceModels.Legacy.Publish3x.TimeSeriesDataCorrectedServiceRequest;
using NodaTime;
using ServiceStack.Logging;

namespace PointZilla
{
    public class ExternalPointsReader
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public ExternalPointsReader(Context context)
        {
            Context = context;
        }

        public List<ReflectedTimeSeriesPoint> LoadPoints()
        {
            var server = !string.IsNullOrEmpty(Context.SourceTimeSeries.Server) ? Context.SourceTimeSeries.Server : Context.Server;
            var username = !string.IsNullOrEmpty(Context.SourceTimeSeries.Username) ? Context.SourceTimeSeries.Username : Context.Username;
            var password = !string.IsNullOrEmpty(Context.SourceTimeSeries.Password) ? Context.SourceTimeSeries.Password : Context.Password;

            using (var client = AquariusClient.CreateConnectedClient(server, username, password))
            {
                Log.Info($"Connected to {server} ({client.ServerVersion})");

                return client.ServerVersion.IsLessThan(MinimumNgVersion)
                    ? LoadPointsFrom3X(client)
                    : LoadPointsFromNg(client);
            }
        }

        private static readonly AquariusServerVersion MinimumNgVersion = AquariusServerVersion.Create("14");

        private List<ReflectedTimeSeriesPoint> LoadPointsFromNg(IAquariusClient client)
        {
            var timeSeriesInfo = client.GetTimeSeriesInfo(Context.SourceTimeSeries.Identifier);

            var points = client.Publish.Get(new TimeAlignedDataServiceRequest
                {
                    TimeSeriesUniqueIds = new List<Guid> { timeSeriesInfo.UniqueId },
                    QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                    QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
                })
                .Points
                .Select(p => new ReflectedTimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp),
                    Value = p.NumericValue1,
                    GradeCode = p.GradeCode1.HasValue ? (int)p.GradeCode1 : (int?)null,
                    Qualifiers = QualifiersParser.Parse(p.Qualifiers1)
                })
                .ToList();

            Log.Info($"Loaded {points.Count} points from {timeSeriesInfo.Identifier}");

            return points;
        }

        private List<ReflectedTimeSeriesPoint> LoadPointsFrom3X(IAquariusClient client)
        {
            var points = client.Publish.Get(new Get3xCorrectedData
                {
                    TimeSeriesIdentifier = Context.SourceTimeSeries.Identifier,
                    QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                    QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
                })
                .Points
                .Select(p => new ReflectedTimeSeriesPoint
                {
                    Time = Instant.FromDateTimeOffset(p.Timestamp),
                    Value = p.Value,
                    GradeCode = p.Grade
                })
                .ToList();

            Log.Info($"Loaded {points.Count} points from {Context.SourceTimeSeries.Identifier}");

            return points;
        }
    }
}
