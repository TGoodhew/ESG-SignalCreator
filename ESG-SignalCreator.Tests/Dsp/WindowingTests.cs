using System;
using EsgSignalCreator.Dsp;
using Xunit;

namespace EsgSignalCreator.Tests.Dsp
{
    public class WindowingTests
    {
        [Fact]
        public void Hann_endpoints_zero_midpoint_one()
        {
            var w = Windowing.Hann(101);
            Assert.Equal(0.0, w[0], 12);
            Assert.Equal(0.0, w[w.Length - 1], 12);
            Assert.Equal(1.0, w[w.Length / 2], 12); // index 50, exact midpoint of 101
        }

        [Fact]
        public void Rectangular_is_all_ones()
        {
            var w = Windowing.Rectangular(16);
            Assert.Equal(16, w.Length);
            foreach (var v in w) Assert.Equal(1.0, v, 12);
        }

        [Fact]
        public void Hamming_endpoints_are_point_zero_eight()
        {
            var w = Windowing.Hamming(64);
            Assert.Equal(0.08, w[0], 6);
            Assert.Equal(0.08, w[w.Length - 1], 6);
        }

        [Fact]
        public void Blackman_endpoints_near_zero_and_symmetric()
        {
            var w = Windowing.Blackman(65);
            Assert.True(Math.Abs(w[0]) < 1e-6);
            for (int k = 0; k < w.Length; k++)
                Assert.Equal(w[k], w[w.Length - 1 - k], 12);
        }

        [Fact]
        public void Length_one_returns_single_unit_sample()
        {
            Assert.Equal(new[] { 1.0 }, Windowing.Hann(1));
            Assert.Equal(new[] { 1.0 }, Windowing.Blackman(1));
        }
    }
}
