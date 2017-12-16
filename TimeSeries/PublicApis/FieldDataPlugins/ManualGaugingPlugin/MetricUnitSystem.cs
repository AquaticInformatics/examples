using FieldDataPluginFramework.Units;

namespace ManualGaugingPlugin
{
    public class MetricUnitSystem : UnitSystem
    {
        public MetricUnitSystem()
        {
            //UnitIds are found by calling 
            // - GetUnits in Provisioning API and using the "UnitIdentifier" property
            // - GetUnits in Publish API and using the "Identifier" property
            DistanceUnitId = "m";
            AreaUnitId = "m^2";
            VelocityUnitId = "m/s";
            DischargeUnitId = "m^3/s";
        }
    }
}
