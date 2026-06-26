using System;
using System.Linq;
using EsgSignalCreator.Markers;
using Xunit;

namespace EsgSignalCreator.Tests.Markers
{
    public sealed class MarkerBuilderTests
    {
        [Fact]
        public void FromSpans_SetsExactlyCoveredSamplesToOne_OthersZero()
        {
            var spans = new[]
            {
                new MarkerSpan(2, 4),
                new MarkerSpan(7, 7),
            };

            byte[] markers = MarkerBuilder.FromSpans(10, spans);

            Assert.Equal(10, markers.Length);
            var expectedOn = new[] { 2, 3, 4, 7 };
            for (int i = 0; i < markers.Length; i++)
            {
                byte expected = expectedOn.Contains(i) ? (byte)1 : (byte)0;
                Assert.Equal(expected, markers[i]);
            }
        }

        [Fact]
        public void FromSpans_NullSpans_ReturnsAllZero()
        {
            byte[] markers = MarkerBuilder.FromSpans(5, null);

            Assert.Equal(5, markers.Length);
            Assert.All(markers, b => Assert.Equal((byte)0, b));
        }

        [Fact]
        public void AtStart_SetsOnlyIndexZero()
        {
            byte[] markers = MarkerBuilder.AtStart(6);

            Assert.Equal(6, markers.Length);
            Assert.Equal((byte)1, markers[0]);
            for (int i = 1; i < markers.Length; i++)
                Assert.Equal((byte)0, markers[i]);
        }

        [Fact]
        public void AtStart_ZeroLength_ReturnsEmpty()
        {
            byte[] markers = MarkerBuilder.AtStart(0);
            Assert.Empty(markers);
        }

        [Fact]
        public void EveryN_SetsIndices_0_n_2n()
        {
            const int n = 3;
            byte[] markers = MarkerBuilder.EveryN(10, n);

            Assert.Equal(10, markers.Length);
            for (int i = 0; i < markers.Length; i++)
            {
                byte expected = (i % n == 0) ? (byte)1 : (byte)0;
                Assert.Equal(expected, markers[i]);
            }
            // Sanity: indices 0, 3, 6, 9 are on.
            Assert.Equal(new[] { 0, 3, 6, 9 }, Enumerable.Range(0, 10).Where(i => markers[i] == 1).ToArray());
        }

        [Fact]
        public void RangeOnOff_ClampsOutOfRangeStopToLengthMinusOne()
        {
            byte[] markers = MarkerBuilder.RangeOnOff(5, 3, 100);

            Assert.Equal(5, markers.Length);
            // Stop clamped to 4 (length-1); samples 3 and 4 on, rest off.
            Assert.Equal(new byte[] { 0, 0, 0, 1, 1 }, markers);
        }

        [Fact]
        public void RangeOnOff_ClampsNegativeStartToZero()
        {
            byte[] markers = MarkerBuilder.RangeOnOff(4, -10, 1);

            Assert.Equal(new byte[] { 1, 1, 0, 0 }, markers);
        }
    }
}
