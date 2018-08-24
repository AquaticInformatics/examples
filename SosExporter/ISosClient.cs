using System;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using SosExporter.Dtos;

namespace SosExporter
{
    public interface ISosClient : IDisposable
    {
        int MaximumPointsPerObservation { get; set; }

        void Connect();
        void Disconnect();
        void ClearDatasource();
        void DeleteDeletedObservations();
        void EnableTransactionalOperations();
        void DisableTransactionalOperations();
        SensorInfo FindExistingSensor(TimeSeriesDataServiceResponse timeSeries);
        void DeleteSensor(TimeSeriesDataServiceResponse timeSeries);
        InsertSensorResponse InsertSensor(TimeSeriesDataServiceResponse timeSeries);
        void InsertObservation(string assignedOffering, LocationDataServiceResponse location, LocationDescription locationDescription, TimeSeriesDataServiceResponse timeSeries, TimeSeriesDescription timeSeriesDescription);
    }
}
