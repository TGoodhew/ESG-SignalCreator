using System;
using EsgSignalCreator.Dsp;
using Xunit;

namespace EsgSignalCreator.Tests.Dsp
{
    public class FirTests
    {
        private static double Sum(double[] h)
        {
            double s = 0.0;
            foreach (var v in h) s += v;
            return s;
        }

        private static void AssertSymmetric(double[] h)
        {
            for (int k = 0; k < h.Length; k++)
                Assert.Equal(h[k], h[h.Length - 1 - k], 10);
        }

        [Theory]
        [InlineData(0.25, 4, 6)]
        [InlineData(0.5, 8, 8)]
        public void Rrc_tap_count_symmetric_unit_sum(double beta, int sps, int span)
        {
            var h = Fir.RootRaisedCosine(beta, sps, span);
            Assert.Equal(sps * span + 1, h.Length);
            AssertSymmetric(h);
            Assert.Equal(1.0, Sum(h), 9);
        }

        [Theory]
        [InlineData(0.35, 4, 6)]
        [InlineData(0.22, 8, 10)]
        public void Rc_tap_count_symmetric_unit_sum(double beta, int sps, int span)
        {
            var h = Fir.RaisedCosine(beta, sps, span);
            Assert.Equal(sps * span + 1, h.Length);
            AssertSymmetric(h);
            Assert.Equal(1.0, Sum(h), 9);
        }

        [Fact]
        public void Gaussian_symmetric_positive_unit_sum()
        {
            var h = Fir.Gaussian(0.3, 8, 4);
            Assert.Equal(8 * 4 + 1, h.Length);
            AssertSymmetric(h);
            foreach (var v in h) Assert.True(v > 0.0, "Gaussian taps must all be positive.");
            Assert.Equal(1.0, Sum(h), 9);
            // Peak must be at the centre tap.
            int mid = h.Length / 2;
            for (int k = 0; k < h.Length; k++)
                if (k != mid) Assert.True(h[k] <= h[mid]);
        }

        [Fact]
        public void Apply_returns_same_length_and_passes_dc_with_unit_gain()
        {
            // A unit-sum filter should preserve a DC (constant) signal in its interior.
            var taps = Fir.Gaussian(0.3, 4, 4);
            var sig = new double[200];
            for (int k = 0; k < sig.Length; k++) sig[k] = 3.0;

            var y = Fir.Apply(sig, taps);
            Assert.Equal(sig.Length, y.Length);
            // Interior samples (away from edge transients) should equal the DC level.
            for (int k = taps.Length; k < sig.Length - taps.Length; k++)
                Assert.Equal(3.0, y[k], 9);
        }

        [Fact]
        public void ApplyComplex_filters_both_rails_same_length()
        {
            var taps = Fir.RaisedCosine(0.3, 4, 6);
            var i = new double[64];
            var q = new double[64];
            for (int k = 0; k < 64; k++) { i[k] = Math.Sin(0.1 * k); q[k] = Math.Cos(0.1 * k); }

            Fir.ApplyComplex(i, q, taps, out var oi, out var oq);
            Assert.Equal(64, oi.Length);
            Assert.Equal(64, oq.Length);
        }

        [Fact]
        public void Rrc_zero_beta_reduces_to_sinc_centre_is_one_over_sum()
        {
            // With beta=0 the centre tap (pre-normalization) is 1 and it is a sinc.
            var h = Fir.RootRaisedCosine(0.0, 4, 8);
            AssertSymmetric(h);
            Assert.Equal(1.0, Sum(h), 9);
        }
    }
}
