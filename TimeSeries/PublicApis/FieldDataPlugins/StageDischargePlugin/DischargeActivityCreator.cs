using System;
using Server.BusinessInterfaces.FieldDataPluginCore;
using Server.BusinessInterfaces.FieldDataPluginCore.Context;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.DischargeActivities;
using StageDischargePlugin.FileData;
using Measurement = Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Measurement;

namespace StageDischargePlugin
{
    public class DischargeActivityCreator
    {
        private readonly LocationInfo _location;
        private readonly FieldVisitRecord _fieldVisit;
        private readonly ILog _log;

        public DischargeActivityCreator(FieldVisitRecord record, LocationInfo location, ILog logger)
        {
            _fieldVisit = record;
            _location = location;
            _log = logger;
        }

        public DischargeActivity Create()
        {
            //NOTE: Timestamps are specified as DateTimeOffset, which requires UTC-offset.
            //If field data file timestamps do not include UTC-offset, plugin can create DateTimeOffset objects using location's UTC-offset.
            var startTime = new DateTimeOffset(_fieldVisit.StartDate, _location.UtcOffset);
            var endTime = new DateTimeOffset(_fieldVisit.EndDate, _location.UtcOffset);
            var interval = new DateTimeInterval(startTime, endTime);

            var factory = new DischargeActivityFactory(_fieldVisit.UnitSystem);
            var dischargeActivity = factory.CreateDischargeActivity(interval, _fieldVisit.DischargeActivity.Discharge);
            _log.Info(
                $"Creating discharge activity from {interval.Start} to {interval.End} with discharge value = {_fieldVisit.DischargeActivity.Discharge}");

            AddGageHeightMeasurements(interval, dischargeActivity);
            dischargeActivity.Comments = _fieldVisit.DischargeActivity.Comments;
            dischargeActivity.Party = _fieldVisit.DischargeActivity.Party;
            dischargeActivity.MeasurementId = _fieldVisit.DischargeActivity.MeasurementId;

            return dischargeActivity;
        }

        private void AddGageHeightMeasurements(DateTimeInterval measurementInterval, DischargeActivity dischargeActivity)
        {
            var startGageHeight = new Measurement(_fieldVisit.DischargeActivity.StartGageHeight,
                _fieldVisit.UnitSystem.DistanceUnitId);
            var endGageHeight = new Measurement(_fieldVisit.DischargeActivity.EndGageHeight,
                _fieldVisit.UnitSystem.DistanceUnitId);

            dischargeActivity.GageHeightMeasurements.Add(new GageHeightMeasurement(startGageHeight,
                measurementInterval.Start));
            dischargeActivity.GageHeightMeasurements.Add(new GageHeightMeasurement(endGageHeight,
                measurementInterval.End));
        }
    }
}
