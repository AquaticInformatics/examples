using NodaTime;

namespace TotalDischargeExternalProcessor
{
    // Use this simple representation to avoid needless null-checks on times & values
    public class Point
    {
        public Instant Time { get; set; }
        public double Value { get; set; }
    }
}
