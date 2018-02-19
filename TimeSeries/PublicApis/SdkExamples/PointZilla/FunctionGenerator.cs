using System;
using System.Collections.Generic;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using NodaTime;
using ServiceStack.Logging;

namespace PointZilla
{
    public class FunctionGenerator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public FunctionGenerator(Context context)
        {
            Context = context;
        }

        public List<ReflectedTimeSeriesPoint> CreatePoints()
        {
            var points = new List<ReflectedTimeSeriesPoint>();

            var pointCount = Context.NumberOfPoints > 0
                ? Context.NumberOfPoints
                : (int)(Context.NumberOfPeriods * Context.FunctionPeriod);

            for (var i = 0; i < pointCount; ++i)
            {
                points.Add(new ReflectedTimeSeriesPoint
                {
                    Time = Context.StartTime.PlusTicks(i * Duration.FromTimeSpan(Context.PointInterval).Ticks),
                    Value = Context.FunctionOffset + Context.FunctionScalar
                            * GeneratorFunctions[Context.FunctionType](i + Context.FunctionPhase, Context.FunctionPeriod),
                    GradeCode = Context.GradeCode,
                    Qualifiers = Context.Qualifiers
                });
            }

            Log.Info($"Generated {pointCount} {Context.FunctionType} points.");

            return points;
        }

        private static readonly Dictionary<FunctionType, Func<double, double, double>> GeneratorFunctions = new Dictionary<FunctionType, Func<double, double, double>>
        {
            {FunctionType.Linear,GenerateLinearValue},
            {FunctionType.SawTooth,GenerateSawToothValue},
            {FunctionType.SineWave,GenerateSineWaveValue},
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

        private static double GenerateSineWaveValue(double iteration, double period)
        {
            return Math.Sin(2.0 * Math.PI * iteration / period);
        }
    }
}
