using System;
using System.Collections.Generic;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using SosExporter.Dtos;

namespace SosExporter
{
    public interface ISosClient : IDisposable
    {
        void Connect();
        void Disconnect();
        void ClearDatasource();
        void DeleteDeletedObservations();
        void EnableTransactionalOperations();
        void DisableTransactionalOperations();
        SensorInfo FindExistingSensor(TimeSeriesDescription timeSeriesDescription);
        void DeleteSensor(TimeSeriesDataServiceResponse timeSeries);
        InsertSensorResponse InsertSensor(TimeSeriesDataServiceResponse timeSeries);
        void InsertObservation(string assignedOffering, LocationDataServiceResponse location, LocationDescription locationDescription, TimeSeriesDataServiceResponse timeSeries);
        List<TimeSeriesPoint> GetObservations(TimeSeriesDescription timeSeriesDescription, DateTimeOffset startTime, DateTimeOffset endTime);
    }
}
