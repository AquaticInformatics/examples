using System;
using System.Collections.Generic;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using NodaTime;
using ServiceStack.Logging;

namespace PointZilla
{
    public class WaveformGenerator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public WaveformGenerator(Context context)
        {
            Context = context;
        }

        public List<ReflectedTimeSeriesPoint> GeneratePoints()
        {
            var points = new List<ReflectedTimeSeriesPoint>();

            var pointCount = Context.NumberOfPoints > 0
                ? Context.NumberOfPoints
                : (int)(Context.NumberOfPeriods * Context.WaveformPeriod);

            for (var i = 0; i < pointCount; ++i)
            {
                points.Add(new ReflectedTimeSeriesPoint
                {
                    Time = Context.StartTime.PlusTicks(i * Duration.FromTimeSpan(Context.PointInterval).Ticks),
                    Value = Context.WaveformOffset + Context.WaveformScalar
                            * GeneratorFunctions[Context.WaveformType](i + Context.WaveformPhase, Context.WaveformPeriod),
                    GradeCode = Context.GradeCode,
                    Qualifiers = Context.Qualifiers
                });
            }

            Log.Info($"Generated {pointCount} {Context.WaveformType} points.");

            return points;
        }

        private static readonly Dictionary<WaveformType, Func<double, double, double>> GeneratorFunctions = new Dictionary<WaveformType, Func<double, double, double>>
        {
            {WaveformType.Linear,GenerateLinearValue},
            {WaveformType.SawTooth,GenerateSawToothValue},
            {WaveformType.SineWave,GenerateSineWaveValue},
            {WaveformType.SquareWave,GenerateSquareWaveValue},
        };

        private static double GenerateLinearValue(double iteration, double period)
        {
            return iteration;
        }

        private static double GenerateSawToothValue(double iteration, double period)
        {
            var modulus = iteration % period;
            return modulus / period;
        }

        private static double GenerateSquareWaveValue(double iteration, double period)
        {
            var modulus = iteration % period;
            return modulus / period < 0.5
                ? 1.0
                : 0;
        }

        private static double GenerateSineWaveValue(double iteration, double period)
        {
            return Math.Sin(2.0 * Math.PI * iteration / period);
        }
    }
}
