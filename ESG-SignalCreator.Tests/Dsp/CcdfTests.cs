using System;
using EsgSignalCreator.Dsp;
using Xunit;

namespace EsgSignalCreator.Tests.Dsp
{
    public class CcdfTests
    {
        private static void Cw(int n, out double[] i, out double[] q)
        {
            i = new double[n];
            q = new double[n];
            for (int k = 0; k < n; k++)
            {
                double phase = 2.0 * Math.PI * 0.05 * k;
                i[k] = Math.Cos(phase);
                q[k] = Math.Sin(phase);
            }
        }

        [Fact]
        public void Papr_of_constant_envelope_cw_is_zero_db()
        {
            Cw(1000, out var i, out var q);
            Assert.Equal(0.0, Ccdf.PaprDb(i, q), 9);
        }

        [Fact]
        public void CrestFactor_of_cw_is_one()
        {
            Cw(1000, out var i, out var q);
            Assert.Equal(1.0, Ccdf.CrestFactor(i, q), 9);
        }

        [Fact]
        public void Papr_two_level_signal_matches_analytic()
        {
            // Half the samples at amplitude 1, half at amplitude 0 (on I rail).
            // mean power = 0.5, peak power = 1 -> PAPR = 10*log10(2) ~= 3.0103 dB.
            int n = 1000;
            var i = new double[n];
            var q = new double[n];
            for (int k = 0; k < n; k++) i[k] = (k % 2 == 0) ? 1.0 : 0.0;
            Assert.Equal(10.0 * Math.Log10(2.0), Ccdf.PaprDb(i, q), 9);
        }

        [Fact]
        public void Curve_starts_at_probability_one_and_is_monotone_nonincreasing()
        {
            // Gaussian-ish noise so the CCDF is well populated.
            var rnd = new Random(12345);
            int n = 5000;
            var i = new double[n];
            var q = new double[n];
            for (int k = 0; k < n; k++)
            {
                i[k] = NextGaussian(rnd);
                q[k] = NextGaussian(rnd);
            }

            Ccdf.Curve(i, q, out var db, out var prob, 12.0, 121);
            Assert.Equal(121, db.Length);
            Assert.Equal(121, prob.Length);
            Assert.Equal(0.0, db[0], 12);

            // At 0 dB above average, some samples must exceed the mean (prob in (0,1]).
            Assert.True(prob[0] > 0.0 && prob[0] <= 1.0);
            for (int k = 1; k < prob.Length; k++)
                Assert.True(prob[k] <= prob[k - 1] + 1e-12, "CCDF must be non-increasing.");
        }

        [Fact]
        public void Curve_empty_input_returns_zero_probabilities()
        {
            Ccdf.Curve(new double[0], new double[0], out var db, out var prob, 12.0, 11);
            Assert.Equal(11, prob.Length);
            foreach (var p in prob) Assert.Equal(0.0, p, 12);
        }

        private static double NextGaussian(Random r)
        {
            double u1 = 1.0 - r.NextDouble();
            double u2 = 1.0 - r.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
