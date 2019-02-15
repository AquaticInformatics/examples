using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ServiceStack;
using ServiceStack.Logging;
using ServiceStack.Text;
using WaterWatchPreProcessor.Dtos;
using WaterWatchPreProcessor.Dtos.WaterWatch;
using WaterWatchPreProcessor.Filters;

namespace WaterWatchPreProcessor
{
    public class Exporter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        private JsonServiceClient Client { get; set; }

        private SavedState SavedState { get; set; } = new SavedState();

        public void Run()
        {
            RestoreSavedState();

            CreateConnectedClient();

            var sensors = GetSensors();

            var nameFilter = new Filter<RegexFilter>(Context.SensorNameFilters);
            var serialFilter = new Filter<RegexFilter>(Context.SensorSerialFilters);

            WriteLine("Iso8601UtcTime, SensorType, SensorSerial, Value");

            var measurementCount = 0;

            foreach (var sensor in sensors)
            {
                if (nameFilter.IsFiltered(f => f.Regex.IsMatch(sensor.Name))
                    || serialFilter.IsFiltered(f => f.Regex.IsMatch(sensor.Serial)))
                    continue;

                var measurements = GetSensorMeasurements(sensor)
                    .ToList();

                var latestMeasurement = measurements.LastOrDefault();

                if (latestMeasurement != null)
                {
                    SavedState.NextMeasurementTimeBySensorSerial[sensor.Serial] = latestMeasurement.Time.AddMilliseconds(1);
                }

                foreach (var measurement in measurements)
                {
                    WriteLine($"{measurement.Time:yyyy-MM-ddTHH:mm:ss.fffZ}, {sensor.SensorType}, {sensor.Serial}, {GetSensorValue(sensor, measurement.RawDistance)}");
                }

                measurementCount += measurements.Count;

                Log.Info($"Wrote {measurements.Count} measurements for sensor '{sensor.Serial}' until {sensor.LatestData?.LastMeasurement?.Time:O}");
            }

            Log.Info($"Wrote {measurementCount} measurements for {sensors.Count} sensors.");

            PersistSavedState();
        }

        private void WriteLine(string message)
        {
            Console.WriteLine(message);
            Log.Info(message);
        }

        private void RestoreSavedState()
        {
            if (!File.Exists(Context.SaveStatePath))
                return;

            SavedState = File.ReadAllText(Context.SaveStatePath).FromJson<SavedState>();
        }

        private void PersistSavedState()
        {
            File.WriteAllText(Context.SaveStatePath, SavedState.ToJson().IndentJson());
        }

        private void CreateConnectedClient()
        {
            var uri = "https://api.waterwatch.io/v1";

            Log.Info($"{Program.GetProgramName()} v{Program.GetExecutingFileVersion()} connecting to {uri} ...");

            Client = new JsonServiceClient(uri)
            {
                AlwaysSendBasicAuthHeader = true,
                UserName = Context.WaterWatchApiKey,
                Password = Context.WaterWatchApiToken
            };
        }

        private IList<Sensor> GetSensors()
        {
            return Client.Get(new GetSensorsRequest
            {
                OrganisationId = Context.WaterWatchOrgId
            });
        }

        private IEnumerable<Measurement> GetSensorMeasurements(Sensor sensor)
        {
            if (!SavedState.NextMeasurementTimeBySensorSerial.TryGetValue(sensor.Serial, out var lastSeenTime))
            {
                lastSeenTime = DateTime.UtcNow.Date.AddDays(-Context.NewSensorSyncDays);
            }

            if (Context.SyncFromUtc.HasValue)
                lastSeenTime = Context.SyncFromUtc.Value;

            var request = new GetMeasurementsRequest
            {
                OrganisationId = sensor.OrganisationId,
                SensorSerial = sensor.Serial,
                StartTime = lastSeenTime,
                Order = "asc"
            };

            do
            {
                var response = Client.Get(request);

                foreach (var measurement in response.Measurements)
                {
                    yield return measurement;
                }

                request.Start = response.Next;

            } while (!string.IsNullOrEmpty(request.Start));
        }

        private double GetSensorValue(Sensor sensor, double rawDistance)
        {
            if (Context.OutputMode == OutputMode.OffsetCorrected && (sensor.DisplayInfo?.OffsetMeasurement.HasValue ?? false))
            {
                return sensor.DisplayInfo.OffsetMeasurement.Value - rawDistance;
            }

            return rawDistance;
        }
    }
}
