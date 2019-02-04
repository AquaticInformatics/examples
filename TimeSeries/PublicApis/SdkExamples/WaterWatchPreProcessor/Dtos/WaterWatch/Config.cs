using System;

namespace WaterWatchPreProcessor.Dtos.WaterWatch
{
    public class Config
    {
        public bool DeltaCompressionEnabled { get; set; }
        public DateTime? LastSchedulerOnTime { get; set; }
        public int MeasurementInterval { get; set; }
        public bool MeasurementTransmissionEnabled { get; set; }
        public bool SchedulerEnabled { get; set; }
        public bool ServerSideAlarm { get; set; }
        public int TransmissionInterval { get; set; }
        public Alarm Alarm { get; set; }
    }
}