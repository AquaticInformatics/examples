using System.Collections.Generic;
using ManualGaugingPlugin.FileData;
using Server.BusinessInterfaces.FieldDataPluginCore;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.ChannelMeasurements;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Verticals;

namespace ManualGaugingPlugin
{
    public class ManualGaugingCreator : CreatorBase<ManualGaugingDischargeSection>
    {
        private readonly DateTimeInterval _measurementPeriod;

        //NOTE: When an enum (e.g. PointVelocityObservationType, DeploymentType) includes both "Unknown" and "Unspecified" enumerations, use the
        //"Unspecified" enumeration.
        public ManualGaugingCreator(FieldVisitRecord record, DateTimeInterval measurementPeriod, ILog logger) : base(record, logger)
        {
            _measurementPeriod = measurementPeriod;
        }

        public override ManualGaugingDischargeSection Create()
        {
            var verticals = GetVerticals();
            var results = new ManualGaugingResultSummary(verticals);

            var factory = new ManualGaugingDischargeSectionFactory(FieldVisit.UnitSystem);
            var manualGauging = factory.CreateManualGaugingDischargeSection(_measurementPeriod,
                results.TotalDischarge);

            manualGauging.DischargeMethod = DischargeMethodType.MeanSection;
            manualGauging.VelocityObservationMethod = FieldVisit.DischargeActivity.ObservationMethodType;
            manualGauging.StartPoint = StartPointType.LeftEdgeOfWater;
            manualGauging.DeploymentMethod = DeploymentMethodType.Unspecified;

            foreach (var vertical in verticals)
            {
                manualGauging.Verticals.Add(vertical);
            }

            manualGauging.WidthValue = results.TotalWidth;
            manualGauging.AreaValue = results.TotalArea;
            manualGauging.VelocityAverageValue = results.MeanVelocity;

            Log.Info($"Creating ManualGaugingDischargeSection with {manualGauging.Verticals.Count} verticals");

            return manualGauging;
        }

        private List<Vertical> GetVerticals()
        {
            var verticalsCreator = new VerticalsCreator(FieldVisit, _measurementPeriod, Log);
            return verticalsCreator.Create();
        }
    }
}
