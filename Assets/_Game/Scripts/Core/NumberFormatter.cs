using System;

namespace YASS.Core
{
    /// <summary>Formats numbers into a reusable char buffer so HUD updates do not allocate strings.</summary>
    public static class NumberFormatter
    {
        /// <summary>Longest output of <see cref="Write"/>: "-9223372036854775808".</summary>
        public const int MaxLongLength = 20;

        /// <summary>Longest output of <see cref="WriteMultiplier"/>: "x" + whole part + ".d".</summary>
        public const int MaxMultiplierLength = 1 + MaxLongLength + 2;

        /// <summary>
        /// Writes <paramref name="value"/> in decimal starting at <paramref name="offset"/>.
        /// Returns the number of chars written.
        /// </summary>
        public static int Write(long value, char[] buffer, int offset = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

            var negative = value < 0;
            var magnitude = negative ? (ulong)(-(value + 1)) + 1 : (ulong)value;

            var length = DigitCount(magnitude) + (negative ? 1 : 0);
            if (buffer.Length - offset < length) throw new ArgumentException("Buffer too small.", nameof(buffer));

            var index = offset + length - 1;
            do
            {
                buffer[index--] = (char)('0' + (int)(magnitude % 10));
                magnitude /= 10;
            } while (magnitude > 0);

            if (negative) buffer[offset] = '-';
            return length;
        }

        /// <summary>
        /// Writes a multiplier with one decimal place, for example "x2.1", starting at <paramref name="offset"/>.
        /// Rounds half away from zero. Returns the number of chars written.
        /// </summary>
        public static int WriteMultiplier(float value, char[] buffer, int offset = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (value < 0f) throw new ArgumentOutOfRangeException(nameof(value));

            var tenths = (long)Math.Round(value * 10.0, MidpointRounding.AwayFromZero);
            var whole = tenths / 10;
            var fraction = (int)(tenths % 10);

            var needed = 1 + DigitCount((ulong)whole) + 2;
            if (buffer.Length - offset < needed) throw new ArgumentException("Buffer too small.", nameof(buffer));

            buffer[offset] = 'x';
            var length = 1 + Write(whole, buffer, offset + 1);
            buffer[offset + length++] = '.';
            buffer[offset + length++] = (char)('0' + fraction);
            return length;
        }

        static int DigitCount(ulong value)
        {
            var digits = 1;
            for (var rest = value / 10; rest > 0; rest /= 10) digits++;
            return digits;
        }
    }
}
