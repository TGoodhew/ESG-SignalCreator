using System;
using EsgSignalCreator.Dsp;
using Xunit;

namespace EsgSignalCreator.Tests.Dsp
{
    public class ResamplerTests
    {
        [Theory]
        [InlineData(10, 25)]
        [InlineData(100, 37)]
        [InlineData(5, 5)]
        public void ResampleLinear_preserves_endpoints_and_length(int srcLen, int targetLen)
        {
            var x = new double[srcLen];
            for (int k = 0; k < srcLen; k++) x[k] = Math.Sin(0.3 * k) + 0.5 * k;

            var y = Resampler.ResampleLinear(x, targetLen);
            Assert.Equal(targetLen, y.Length);
            Assert.Equal(x[0], y[0], 12);
            Assert.Equal(x[srcLen - 1], y[targetLen - 1], 12);
        }

        [Fact]
        public void ResampleLinear_upsampling_a_ramp_stays_linear()
        {
            // A linear ramp resampled linearly must remain the same straight line.
            var x = new double[5];
            for (int k = 0; k < 5; k++) x[k] = 2.0 * k; // 0,2,4,6,8
            var y = Resampler.ResampleLinear(x, 9);
            // positions hit 0..4 in steps of 0.5 -> values 0,1,2,...,8
            for (int k = 0; k < 9; k++)
                Assert.Equal(k * 1.0, y[k], 9);
        }

        [Fact]
        public void ResampleLinear_single_sample_input_fills_constant()
        {
            var y = Resampler.ResampleLinear(new[] { 7.0 }, 4);
            Assert.Equal(4, y.Length);
            foreach (var v in y) Assert.Equal(7.0, v, 12);
        }

        [Fact]
        public void ResampleLinear_target_one_returns_first_sample()
        {
            var y = Resampler.ResampleLinear(new[] { 3.0, 4.0, 5.0 }, 1);
            Assert.Single(y);
            Assert.Equal(3.0, y[0], 12);
        }

        [Fact]
        public void ResampleComplexLinear_resamples_both_rails()
        {
            var i = new double[8];
            var q = new double[8];
            for (int k = 0; k < 8; k++) { i[k] = k; q[k] = -k; }

            Resampler.ResampleComplexLinear(i, q, 20, out var oi, out var oq);
            Assert.Equal(20, oi.Length);
            Assert.Equal(20, oq.Length);
            Assert.Equal(0.0, oi[0], 12);
            Assert.Equal(7.0, oi[19], 12);
            Assert.Equal(0.0, oq[0], 12);
            Assert.Equal(-7.0, oq[19], 12);
        }
    }
}
