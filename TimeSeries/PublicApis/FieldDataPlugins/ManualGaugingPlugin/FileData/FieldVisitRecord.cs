using System;
using FieldDataPluginFramework.Units;

namespace ManualGaugingPlugin.FileData
{
    public class FieldVisitRecord
    {
        //It is best practice to use LocationUniqueId (can be discovered via Publish API or Springboard UI) because it is guaranteed to be unique string.
        //It is also OK to use the LocationIdentifier but be aware that a user can change the LocationIdentifier using LocationManager or Provisioning API.
        public string LocationIdentifier { get; private set; }

        //Ideally, StartDate and EndDate are defined as DateTimeOffset, as timestamps in AQUARIUS are always specified with a UTC-offset.
        //However, it is common that timestamps in files do not include an UTC-offset.
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public DischargeActivityRecord DischargeActivity { get; set; }

        public UnitSystem UnitSystem { get; private set; }

        public FieldVisitRecord(UnitSystem unitSystem, string locationIdentifier, DateTime startDate, DateTime endDate)
        {
            UnitSystem = unitSystem;
            LocationIdentifier = locationIdentifier;
            StartDate = startDate;
            EndDate = endDate;
        }
    }
}
