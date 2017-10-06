using System;
using ManualGaugingPlugin.FileData;
using Server.BusinessInterfaces.FieldDataPluginCore;
using Server.BusinessInterfaces.FieldDataPluginCore.Context;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.ChannelMeasurements;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.DischargeActivities;
using Measurement = Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Measurement;

namespace ManualGaugingPlugin
{
    public class DischargeActivityCreator : CreatorBase<DischargeActivity>
    {
        private readonly LocationInfo _location;

        public DischargeActivityCreator(FieldVisitRecord record, LocationInfo location, ILog logger) : base(record, logger)
        {
            _location = location;
        }

        public override DischargeActivity Create()
        {
            //NOTE: Timestamps are specified as DateTimeOffset, which requires UTC-offset.
            //If field data file timestamps do not include UTC-offset, plugin can create DateTimeOffset objects using location's UTC-offset.
            var startTime = new DateTimeOffset(FieldVisit.StartDate, _location.UtcOffset);
            var endTime = new DateTimeOffset(FieldVisit.EndDate, _location.UtcOffset);
            var interval = new DateTimeInterval(startTime, endTime);

            var dischargeSection = GetManualGaugingDischargeSection(interval);

            var factory = new DischargeActivityFactory(FieldVisit.UnitSystem);
            var dischargeActivity = factory.CreateDischargeActivity(interval, dischargeSection.Discharge.Value);
            Log.Info(
                $"Creating discharge activity from {interval.Start} to {interval.End} with discharge value = {dischargeSection.Discharge.Value}");

            AddGageHeightMeasurements(interval, dischargeActivity);
            dischargeActivity.ChannelMeasurements.Add(dischargeSection);

            return dischargeActivity;
        }

        private void AddGageHeightMeasurements(DateTimeInterval measurementInterval, DischargeActivity dischargeActivity)
        {
            var startGageHeight = new Measurement(FieldVisit.DischargeActivity.StartGageHeight,
                FieldVisit.UnitSystem.DistanceUnitId);
            var endGageHeight = new Measurement(FieldVisit.DischargeActivity.EndGageHeight,
                FieldVisit.UnitSystem.DistanceUnitId);

            dischargeActivity.GageHeightMeasurements.Add(new GageHeightMeasurement(startGageHeight,
                measurementInterval.Start));
            dischargeActivity.GageHeightMeasurements.Add(new GageHeightMeasurement(endGageHeight,
                measurementInterval.End));
        }

        private ManualGaugingDischargeSection GetManualGaugingDischargeSection(DateTimeInterval measurementPeriod)
        {
            var creator = new ManualGaugingCreator(FieldVisit, measurementPeriod, Log);
            return creator.Create();
        }
    }
}
