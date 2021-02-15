using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.TimeSeries.Client.ServiceModels.Acquisition;
using Humanizer;
using NodaTime;
using ServiceStack.Logging;

namespace PointZilla
{
    public class TextGenerator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Context Context { get; }

        public TextGenerator(Context context)
        {
            Context = context;
        }

        public List<TimeSeriesPoint> GeneratePoints()
        {
            var points = new List<TimeSeriesPoint>();

            var isXValueSelected = !string.IsNullOrWhiteSpace(Context.WaveFormTextX);

            var text = isXValueSelected
                ? Context.WaveFormTextX
                : Context.WaveFormTextY;

            text = text.Trim();

            if (string.IsNullOrEmpty(text))
                throw new ExpectedException($"You must set the /{nameof(Context.WaveFormTextX)}= or /{nameof(Context.WaveFormTextY)}= to a non-empty value.");

            var invalidChars = text
                .Where(ch => !VectorFont.Symbols.ContainsKey(ch))
                .ToList();

            if (invalidChars.Any())
                throw new ExpectedException($"Only printable ASCII characters are supported. {"invalid character".ToQuantity(invalidChars.Count)}' detected.");

            var x = 0.0;
            var y = 0.0 + Context.WaveformOffset;

            foreach (var symbol in text.Select(ch => VectorFont.Symbols[ch]))
            {
                var startX = x;
                var startY = y;

                foreach (var point in symbol.Points)
                {
                    points.Add(new TimeSeriesPoint
                    {
                        Time = Context.StartTime.PlusTicks(points.Count * Duration.FromTimeSpan(Context.PointInterval).Ticks),
                        Value = isXValueSelected
                            ? startX + Context.WaveformScalar * point.X
                            : startY + Context.WaveformScalar * point.Y,
                        GradeCode = Context.GradeCode,
                        Qualifiers = Context.Qualifiers
                    });
                }

                x += symbol.Width * Context.WaveformScalar;
            }

            Log.Info($"Generated {"vector-text point".ToQuantity(points.Count)} points. Scatter-plot X vs Y to read the message.");

            return points;
        }
    }
}
