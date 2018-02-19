using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using ServiceStack.Logging;

namespace PointZilla
{
    public class PointsAppender
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public PointsAppender(Context context)
        {
            Context = context;
        }

        public void AppendPoints()
        {
            var points = GetPoints()
                .OrderBy(p => p.Time)
                .ToList();

            using (_client = AquariusClient.CreateConnectedClient(Context.Server, Context.Username, Context.Password))
            {
                Log.Info($"Connected to {Context.Server} ({_client.ServerVersion})");

                Log.Info($"Appending {points.Count} points to {Context.TimeSeries}");

            }
        }

        private IAquariusClient _client;

        private List<ReflectedTimeSeriesPoint> GetPoints()
        {
            if (Context.ManualPoints.Any())
                return Context.ManualPoints;

            if (Context.CsvFiles.Any())
                return new CsvReader(Context)
                    .LoadPoints();

            return new FunctionGenerator(Context)
                .CreatePoints();
        }

        private Guid GetTimeSeriesUniqueId()
        {
            Guid uniqueId;

            if (Guid.TryParse(_timeSeriesIdentifier, out uniqueId))
                return uniqueId;

            var location = ParseLocationIdentifier(_timeSeriesIdentifier);

            var response = _client.Publish.Get(new TimeSeriesDescriptionServiceRequest { LocationIdentifier = location });

            var timeSeriesDescription = response.TimeSeriesDescriptions.FirstOrDefault(t => t.Identifier == _timeSeriesIdentifier);

            if (timeSeriesDescription == null)
                throw new ArgumentException($"Can't find '{_timeSeriesIdentifier}' at location '{location}'");

            return timeSeriesDescription.UniqueId;
        }

    }
}
