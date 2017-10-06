using System;
using System.Collections.Generic;
using System.Linq;
using ManualGaugingPlugin.FileData;
using Server.BusinessInterfaces.FieldDataPluginCore;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.ChannelMeasurements;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Meters;
using Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Verticals;
using VelocityObservation = Server.BusinessInterfaces.FieldDataPluginCore.DataModel.Verticals.VelocityObservation;

namespace ManualGaugingPlugin
{
    public class VerticalsCreator : CreatorBase<List<Vertical>>
    {
        private readonly DateTimeInterval _measurementPeriod;

        private readonly int _startEdgeSequenceNumber;
        private readonly int _endEdgeSequenceNumber;

        public VerticalsCreator(FieldVisitRecord record, DateTimeInterval measurementPeriod, ILog logger)
            : base(record, logger)
        {
            _measurementPeriod = measurementPeriod;

            _startEdgeSequenceNumber = 1;
            _endEdgeSequenceNumber = record.DischargeActivity.ManualGaugings.Count;
        }

        public override List<Vertical> Create()
        {
            var verticals = new List<Vertical>();
            Vertical precedingVertical = null;
            for (var i = 0; i < FieldVisit.DischargeActivity.ManualGaugings.Count; i++)
            {
                var gauging = FieldVisit.DischargeActivity.ManualGaugings[i];
                var sequenceNumber = i + 1;
                var vertical = sequenceNumber == _startEdgeSequenceNumber
                    ? GetStartEdgeVertical(gauging)
                    : GetVertical(gauging, precedingVertical, sequenceNumber);

                verticals.Add(vertical);
                precedingVertical = vertical;
            }

            SetTotalDischargePortionForEachVertical(verticals);
            return verticals;
        }

        private Vertical GetStartEdgeVertical(ManualGaugingRecord gauging)
        {
            return GetVertical(gauging, _startEdgeSequenceNumber, VerticalType.StartEdgeNoWaterBefore);
        }

        private Vertical GetVertical(ManualGaugingRecord gauging, Vertical precedingVertical, int sequenceNumber)
        {
            var verticalType = sequenceNumber == _endEdgeSequenceNumber
                ? VerticalType.EndEdgeNoWaterAfter
                : VerticalType.MidRiver;

            var vertical = GetVertical(gauging, sequenceNumber, verticalType);

            var segment = GetSegment(gauging, vertical.VelocityObservation, precedingVertical);
            vertical.Segment = segment;

            return vertical;
        }

        private Vertical GetVertical(ManualGaugingRecord gauging, int sequenceNumber, VerticalType verticalType)
        {
            var measurementTime = CalculateMeasurementTime();
            var velocityObservation = GetVelocityObservation(gauging);

            Log.Info($"Vertical: {sequenceNumber}");

            return new Vertical
            {
                SequenceNumber = sequenceNumber,
                FlowDirection = FlowDirectionType.Normal,
                IsSoundedDepthEstimated = false,
                MeasurementTime = measurementTime,
                SoundedDepth = gauging.SoundedDepth,
                TaglinePosition = gauging.TaglinePosition,
                EffectiveDepth = gauging.SoundedDepth,
                VerticalType = verticalType,
                MeasurementConditionData = new OpenWaterData(),
                VelocityObservation = velocityObservation
            };
        }

        private DateTimeOffset CalculateMeasurementTime()
        {
            var ticks = _measurementPeriod.End.Subtract(_measurementPeriod.Start).Ticks / 2;
            return _measurementPeriod.Start.AddTicks(ticks);
        }

        private VelocityObservation GetVelocityObservation(ManualGaugingRecord gauging)
        {
            var meterCalibration = GetMeterCalibration();
            var depthObservations = GetVelocityDepthObservations(gauging, meterCalibration);
            var meanVelocity = CalculateMeanVelocity(depthObservations);
            var observationMethod = FieldVisit.DischargeActivity.ObservationMethodType;

            var velocityObservation = new VelocityObservation
            {
                MeanVelocity = meanVelocity,
                VelocityObservationMethod = observationMethod,
                MeterCalibration = meterCalibration,
                DeploymentMethod = DeploymentMethodType.Unspecified
            };

            foreach (var observation in depthObservations)
            {
                velocityObservation.Observations.Add(observation);
            }

            Log.Info($"VelocityObservation: VMV={meanVelocity}, observationMethod={observationMethod}");

            return velocityObservation;
        }

        private MeterCalibration GetMeterCalibration()
        {
            var meterCreator = new MeterCalibrationCreator(FieldVisit, Log);
            return meterCreator.Create();
        }

        private List<VelocityDepthObservation> GetVelocityDepthObservations(ManualGaugingRecord gauging,
            MeterCalibration meterCalibration)
        {
            return gauging.Observations.Select(observation =>
            {
                var revolutionCount = observation.Revolutions;
                var observationInterval = observation.IntervalInSeconds;
                if (observation.Revolutions == 0 || observation.IntervalInSeconds == 0)
                {
                    revolutionCount = 0;
                    observationInterval = 0;
                }

                var velocity = CalculateDepthObservationVelocity(revolutionCount, observationInterval, meterCalibration);

                return new VelocityDepthObservation
                {
                    Depth = observation.ObservationDepth,
                    IsVelocityEstimated = false,
                    RevolutionCount = revolutionCount,
                    Velocity = velocity,
                    ObservationInterval = observationInterval
                };
            }).ToList();
        }

        private double CalculateDepthObservationVelocity(int revolutionCount, int observationInterval,
            MeterCalibration meterCalibration)
        {
            var velocity = 0.0;

            var frequency = (float) revolutionCount/observationInterval;
            foreach (var equation in meterCalibration.Equations)
            {
                var rangeStart = equation.RangeStart.GetValueOrDefault();
                var rangeEnd = equation.RangeEnd.GetValueOrDefault();

                if (frequency > rangeStart &&
                    (frequency < rangeEnd || DoubleHelper.HasMinimalDifference(frequency, rangeEnd)))
                {
                    velocity = frequency*equation.Slope + equation.Intercept;
                    break;
                }
            }

            Log.Info(
                $"VelocityDepthObservation: Revolutions={revolutionCount}, Interval={observationInterval}, Frequency={frequency} m/s, Velocity={velocity} m/s");

            return velocity;
        }

        private static double CalculateMeanVelocity(IReadOnlyCollection<VelocityDepthObservation> observations)
        {
            return !observations.Any() ? 0.0d : observations.Average(s => s.Velocity);
        }

        private Segment GetSegment(ManualGaugingRecord gauging, VelocityObservation velocityObservation, Vertical precedingVertical)
        {
            var segmentVelocity = CalculateSegmentVelocity(velocityObservation, precedingVertical);
            var segmentWidth = CalculateSegmentWidth(gauging, precedingVertical);
            var segmentDepth = CalculateSegmentDepth(gauging, precedingVertical);
            var segmentArea = segmentDepth * segmentWidth;
            var segmentDischarge = segmentVelocity * segmentArea;

            Log.Info(
                $"Segment: Velocity={segmentVelocity} m/s, Depth={segmentDepth} m, Width={segmentWidth} m, Area={segmentArea} m^2, Discharge={segmentDischarge} m^3/s");

            return new Segment
            {
                Area = segmentArea,
                Discharge = segmentDischarge,
                IsDischargeEstimated = false,
                Velocity = segmentVelocity,
                Width = segmentWidth
            };
        }

        private static double CalculateSegmentVelocity(VelocityObservation velocityObservation,
            Vertical precedingVertical)
        {
            return (precedingVertical.VelocityObservation.MeanVelocity + velocityObservation.MeanVelocity) / 2;
        }

        private static double CalculateSegmentWidth(ManualGaugingRecord gauging, Vertical precedingVertical)
        {
            return gauging.TaglinePosition - precedingVertical.TaglinePosition;
        }

        private static double CalculateSegmentDepth(ManualGaugingRecord gauging, Vertical precedingVertical)
        {
            return (gauging.SoundedDepth + precedingVertical.SoundedDepth) / 2;
        }

        private static void SetTotalDischargePortionForEachVertical(List<Vertical> verticals)
        {
            var totalDischarge = verticals.Where(v => v.Segment != null).Sum(v => v.Segment.Discharge);

            foreach (var v in verticals)
            {
                if (v.Segment != null)
                {
                    v.Segment.TotalDischargePortion = v.Segment.Discharge / totalDischarge * 100;
                }
            }
        }
    }
}
