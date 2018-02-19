using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
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

                // TODO: Make this work for 3.X systems too

                var timeSeriesInfo = client.GetTimeSeriesInfo(Context.SourceTimeSeries.Identifier);

                var request = new TimeAlignedDataServiceRequest
                {
                    TimeSeriesUniqueIds = new List<Guid> {timeSeriesInfo.UniqueId},
                    QueryFrom = Context.SourceQueryFrom?.ToDateTimeOffset(),
                    QueryTo = Context.SourceQueryTo?.ToDateTimeOffset()
                };

                var points = client.Publish.Get(request).Points
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
        }
    }
}
