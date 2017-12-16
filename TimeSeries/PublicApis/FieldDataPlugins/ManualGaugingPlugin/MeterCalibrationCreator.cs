using System.Collections.Generic;
using System.Linq;
using FieldDataPluginFramework;
using FieldDataPluginFramework.DataModel.Meters;
using ManualGaugingPlugin.FileData;

namespace ManualGaugingPlugin
{
    public class MeterCalibrationCreator : CreatorBase<MeterCalibration>
    {
        public MeterCalibrationCreator(FieldVisitRecord record, ILog logger) : base(record, logger)
        {
        }

        public override MeterCalibration Create()
        {
            var calibration = new MeterCalibration
            {
                Model = FieldVisit.DischargeActivity.Meter.Model,
                Manufacturer = FieldVisit.DischargeActivity.Meter.Manufacturer,
                SerialNumber = FieldVisit.DischargeActivity.Meter.SerialNumber,
                MeterType = MeterType.Unspecified
            };

            foreach (var equation in GetMeterEquations())
            {
                calibration.Equations.Add(equation);
            }

            return calibration;
        }

        public List<MeterCalibrationEquation> GetMeterEquations()
        {
            return FieldVisit.DischargeActivity.Meter.Equations.Select(equation => new MeterCalibrationEquation
            {
                Intercept = equation.Intercept,
                InterceptUnitId = FieldVisit.UnitSystem.DistanceUnitId,
                RangeStart = equation.StartRange,
                RangeEnd = equation.EndRange,
                Slope = equation.Slope
            }).ToList();
        }
    }
}
