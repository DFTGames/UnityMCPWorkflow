using System;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class VisualCuesTests
    {
        [TestCase(0f, true)]
        [TestCase(-1f, true)]
        [TestCase(1.0f, false)]   // phase 10.0: first half of cycle
        [TestCase(1.06f, true)]   // phase 10.6
        [TestCase(1.02f, false)]  // phase 10.2
        public void IsBlinkVisible(float remaining, bool expected)
        {
            Assert.That(VisualCues.IsBlinkVisible(remaining, 10f), Is.EqualTo(expected));
        }

        [Test]
        public void IsBlinkVisible_RejectsNonPositiveRate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VisualCues.IsBlinkVisible(1f, 0f));
        }
    }

    public class NumberFormatterTests
    {
        static string Format(long value)
        {
            var buffer = new char[NumberFormatter.MaxLongLength];
            return new string(buffer, 0, NumberFormatter.Write(value, buffer));
        }

        static string FormatMultiplier(float value)
        {
            var buffer = new char[NumberFormatter.MaxMultiplierLength];
            return new string(buffer, 0, NumberFormatter.WriteMultiplier(value, buffer));
        }

        [TestCase(0L, "0")]
        [TestCase(7L, "7")]
        [TestCase(1234567L, "1234567")]
        [TestCase(-42L, "-42")]
        [TestCase(long.MaxValue, "9223372036854775807")]
        [TestCase(long.MinValue, "-9223372036854775808")]
        public void Write_FormatsDecimal(long value, string expected)
        {
            Assert.That(Format(value), Is.EqualTo(expected));
        }

        [Test]
        public void Write_AtOffset_LeavesPrefixIntact()
        {
            var buffer = new[] { 'L', 'v', ' ', '?', '?' };

            var length = NumberFormatter.Write(42, buffer, 3);

            Assert.That(new string(buffer, 0, 3 + length), Is.EqualTo("Lv 42"));
        }

        [Test]
        public void Write_RejectsSmallBufferOrBadOffset()
        {
            Assert.Throws<ArgumentException>(() => NumberFormatter.Write(12345, new char[4]));
            Assert.Throws<ArgumentException>(() => NumberFormatter.Write(12, new char[3], 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.Write(1, new char[3], -1));
            Assert.Throws<ArgumentNullException>(() => NumberFormatter.Write(1, null));
        }

        [TestCase(1f, "x1.0")]
        [TestCase(2.1f, "x2.1")]
        [TestCase(3f, "x3.0")]
        [TestCase(1.25f, "x1.3")]
        [TestCase(12.5f, "x12.5")]
        public void WriteMultiplier_OneDecimalPlace(float value, string expected)
        {
            Assert.That(FormatMultiplier(value), Is.EqualTo(expected));
        }

        [Test]
        public void WriteMultiplier_RejectsNegativeOrSmallBuffer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.WriteMultiplier(-1f, new char[10]));
            Assert.Throws<ArgumentException>(() => NumberFormatter.WriteMultiplier(10f, new char[4]));
            Assert.Throws<ArgumentOutOfRangeException>(() => NumberFormatter.WriteMultiplier(1f, new char[10], -1));
            Assert.Throws<ArgumentException>(() => NumberFormatter.WriteMultiplier(1f, new char[10], 8),
                "the offset must leave room for the whole multiplier");
        }

        [Test]
        public void WriteMultiplier_WritesAfterAPrefix()
        {
            // The results screen writes "Best chain x2.1" into one buffer (GDD "UI Flow and Screens").
            var buffer = new char[32];
            const string prefix = "Best chain ";
            prefix.CopyTo(0, buffer, 0, prefix.Length);

            var length = prefix.Length + NumberFormatter.WriteMultiplier(2.1f, buffer, prefix.Length);

            Assert.That(new string(buffer, 0, length), Is.EqualTo("Best chain x2.1"));
        }
    }
}
