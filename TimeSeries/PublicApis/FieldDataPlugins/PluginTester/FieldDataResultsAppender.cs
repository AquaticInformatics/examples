using System;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.CrossSection;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.Results;

namespace PluginTester
{
    public class FieldDataResultsAppender : IFieldDataResultsAppender
    {
        private static readonly Guid DummyUniqueId = new Guid("{1BF7F45B-5361-474F-9210-FAC4F29FB4BB}");

        public static LocationInfo CreateLocationInfo(string locationIdentifier)
        {
            const long dummyLocationId = 0;
            const double dummyUtcOffset = 0;

            return InternalConstructor<LocationInfo>.Invoke(
                $"NameOf{locationIdentifier}",
                locationIdentifier,
                dummyLocationId,
                DummyUniqueId,
                dummyUtcOffset);
        }

        public LocationInfo LocationInfo { get; set; }

        public AppendedResults AppendedResults { get; } = new AppendedResults
        {
            FrameworkAssemblyQualifiedName = typeof(IFieldDataPlugin).AssemblyQualifiedName
        };

        public LocationInfo GetLocationByIdentifier(string locationIdentifier)
        {
            if (LocationInfo == null)
                return CreateLocationInfo(locationIdentifier);

            if (locationIdentifier == LocationInfo.LocationIdentifier)
                return LocationInfo;

            throw new ArgumentException($"Location {locationIdentifier} does not exist");
        }

        public LocationInfo GetLocationByUniqueId(string uniqueId)
        {
            if (LocationInfo == null)
                return CreateLocationInfo(uniqueId);

            if (uniqueId == LocationInfo.UniqueId)
                return LocationInfo;

            throw new ArgumentException($"Location {uniqueId} does not exist");
        }

        public FieldVisitInfo AddFieldVisit(LocationInfo location, FieldVisitDetails fieldVisitDetails)
        {
            var fieldVisitInfo = InternalConstructor<FieldVisitInfo>.Invoke(location, fieldVisitDetails);

            AppendedResults.AppendedVisits.Add(fieldVisitInfo);

            return fieldVisitInfo;
        }

        public void AddDischargeActivity(FieldVisitInfo fieldVisit, DischargeActivity dischargeActivity)
        {
            fieldVisit.DischargeActivities.Add(dischargeActivity);
        }

        public void AddCrossSectionSurvey(FieldVisitInfo fieldVisit, CrossSectionSurvey crossSectionSurvey)
        {
            fieldVisit.CrossSectionSurveys.Add(crossSectionSurvey);
        }

        public void AddReading(FieldVisitInfo fieldVisit, Reading reading)
        {
            fieldVisit.Readings.Add(reading);
        }
    }
}
