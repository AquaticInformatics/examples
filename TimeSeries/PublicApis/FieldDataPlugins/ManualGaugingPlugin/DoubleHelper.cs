using System;

namespace ManualGaugingPlugin
{
    public static class DoubleHelper
    {
        private const int MaxUlps = 4;

        // Adapted from https://msdn.microsoft.com/en-us/library/ya2zha7s(v=vs.110).aspx
        public static bool HasMinimalDifference(double value1, double value2)
        {
            var lValue1 = BitConverter.DoubleToInt64Bits(value1);
            var lValue2 = BitConverter.DoubleToInt64Bits(value2);

            // If the signs are different, return false except for +0 and -0.
            if ((lValue1 >> 63) != (lValue2 >> 63))
            {
                return value1 == value2;
            }

            var diff = Math.Abs(lValue1 - lValue2);
            return diff <= MaxUlps;
        }
    }
}
