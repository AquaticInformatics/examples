using Server.BusinessInterfaces.FieldDataPluginCore.Units;

namespace StageDischargePlugin
{
    public class MetricUnitSystem : UnitSystem
    {
        public MetricUnitSystem()
        {
            //UnitIds are found by calling 
            // - GetUnits in Provisioning API and using the "UnitIdentifier" property
            // - GetUnits in Publish API and using the "Identifier" property
            //The "UnitIdentifier" property are system ids and cannot be modified.  So, it is safe to hardcode these values into your plugin.
            DistanceUnitId = "m";
            AreaUnitId = "m^2";
            VelocityUnitId = "m/s";
            DischargeUnitId = "m^3/s";
        }
    }
}
