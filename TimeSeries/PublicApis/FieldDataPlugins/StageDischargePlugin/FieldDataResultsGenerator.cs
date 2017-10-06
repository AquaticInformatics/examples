using System;
using Server.BusinessInterfaces.FieldDataPluginCore;
using Server.BusinessInterfaces.FieldDataPluginCore.Context;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.DischargeActivities;
using Server.BusinessInterfaces.FieldDataPluginCore.Results;
using StageDischargePlugin.FileData;

namespace StageDischargePlugin
{
    public class FieldDataResultsGenerator
    {
        private readonly IFieldDataResultsAppender _resultsAppender;
        private readonly LocationInfo _location;
        private readonly ILog _log;

        public FieldDataResultsGenerator(IFieldDataResultsAppender resultsAppender, LocationInfo location, ILog log)
        {
            _resultsAppender = resultsAppender;
            _location = location;
            _log = log;
        }

        public void GenerateFieldDataResults(FieldVisitRecord record)
        {
            var visitDetails = CreateFieldVisitDetails(record);
            var fieldVisit = _resultsAppender.AddFieldVisit(_location, visitDetails);
            _log.Info(
                $"Creating field visit with start date {visitDetails.StartDate} and end date {visitDetails.EndDate} for location {_location.LocationIdentifier}");

            var dischargeActivity = CreateDischargeActivity(record);
            _resultsAppender.AddDischargeActivity(fieldVisit, dischargeActivity);
        }

        private FieldVisitDetails CreateFieldVisitDetails(FieldVisitRecord record)
        {
            //NOTE: Timestamps are specified as DateTimeOffset, which requires UTC-offset.
            //If field data file timestamps do not include UTC-offset, plugin can create DateTimeOffset objects using location's UTC-offset.
            var startTime = new DateTimeOffset(record.StartDate, _location.UtcOffset);
            var endTime = new DateTimeOffset(record.EndDate, _location.UtcOffset);
            var interval = new DateTimeInterval(startTime, endTime);

            return new FieldVisitDetails(interval);
        }

        private DischargeActivity CreateDischargeActivity(FieldVisitRecord record)
        {
            var creator = new DischargeActivityCreator(record, _location, _log);
            return creator.Create();
        }
    }
}
