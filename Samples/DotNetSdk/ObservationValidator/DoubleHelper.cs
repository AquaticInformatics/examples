using System;
using System.Runtime.InteropServices;

namespace ObservationValidator
{
    //Original source for this util class is AQTS' Common Utils.
    public static class DoubleHelper
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleInt64Union
        {
            [FieldOffset(0)] public double Double;
            [FieldOffset(0)] public Int64 Int64;

            public bool IsNegative => (Int64 >> 63) != 0L;
        }

        public const int MaxUlps = 4;

        /// <summary>
        /// Compare doubles for equality using units in the last place (ULP) comparison.
        /// </summary>
        /// <remarks>
        /// This implementation assumes doubles are implemented in the IEEE 754 floating-point format.
        /// Source: https://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
        /// </remarks>
        public static bool AreEqual(double firstValue, double secondValue)
        {
            var leftUnion = new DoubleInt64Union();
            var rightUnion = new DoubleInt64Union();

            leftUnion.Double = firstValue;
            rightUnion.Double = secondValue;

            if (leftUnion.IsNegative != rightUnion.IsNegative)
            {
                // This is used deliberately to ensure 0.0 and -0.0 are considered equal
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (leftUnion.Double == rightUnion.Double)
                {
                    return true;
                }
                return false;
            }

            var ulpsDiff = Math.Abs(leftUnion.Int64 - rightUnion.Int64);
            return ulpsDiff <= MaxUlps;
        }

        public static bool IsLessThan(double firstValue, double secondValue)
        {
            return !AreEqual(firstValue, secondValue) && firstValue < secondValue;
        }

        public static bool IsGreaterThan(double firstValue, double secondValue)
        {
            return !AreEqual(firstValue, secondValue) && firstValue > secondValue;
        }

        public static bool IsGreaterOrEqual(double firstValue, double secondValue)
        {
            return AreEqual(firstValue, secondValue) || firstValue > secondValue;
        }

        public static bool IsLessOrEqual(double firstValue, double secondValue)
        {
            return AreEqual(firstValue, secondValue) || firstValue < secondValue;
        }
    }
}
