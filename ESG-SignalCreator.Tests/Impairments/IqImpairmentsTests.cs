using System;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Impairments
{
    public class IqImpairmentsTests
    {
        private static WaveformModel MakeInput()
        {
            var i = new float[] { 0.1f, 0.5f, -0.3f, 0.8f, -0.6f, 0.2f };
            var q = new float[] { 0.4f, -0.2f, 0.7f, -0.5f, 0.3f, -0.9f };
            return new WaveformModel(i, q, 10e6, "test");
        }

        private static double Mean(float[] x)
        {
            double s = 0.0;
            foreach (var v in x) s += v;
            return s / x.Length;
        }

        [Fact]
        public void Identity_config_returns_equal_samples()
        {
            var input = MakeInput();
            var cfg = new IqImpairmentConfig();

            var output = IqImpairments.Apply(input, cfg);

            Assert.Equal(input.Length, output.Length);
            Assert.Equal(input.SampleRateHz, output.SampleRateHz);
            Assert.Equal(input.Name, output.Name);
            for (int k = 0; k < input.Length; k++)
            {
                Assert.Equal(input.I[k], output.I[k], 6);
                Assert.Equal(input.Q[k], output.Q[k], 6);
            }
        }

        [Fact]
        public void Does_not_mutate_input()
        {
            var input = MakeInput();
            var origI = (float[])input.I.Clone();
            var origQ = (float[])input.Q.Clone();
            var cfg = new IqImpairmentConfig
            {
                GainImbalanceDb = 3.0,
                QuadratureSkewDeg = 5.0,
                DcOffsetI = 0.1,
                DcOffsetQ = -0.05,
                SwapIq = true
            };

            IqImpairments.Apply(input, cfg);

            for (int k = 0; k < input.Length; k++)
            {
                Assert.Equal(origI[k], input.I[k]);
                Assert.Equal(origQ[k], input.Q[k]);
            }
        }

        [Fact]
        public void Gain_imbalance_scales_I_up_and_Q_down()
        {
            var input = MakeInput();
            double db = 6.0;
            var cfg = new IqImpairmentConfig { GainImbalanceDb = db };

            var output = IqImpairments.Apply(input, cfg);

            // Ratio of (I gain) / (Q gain) should equal 10^(db/20).
            double expectedRatio = Math.Pow(10.0, db / 20.0);
            // Use a sample with non-trivial I and Q.
            int k = 1; // I=0.5, Q=-0.2
            double iGain = output.I[k] / input.I[k];
            double qGain = output.Q[k] / input.Q[k];

            Assert.True(iGain > 1.0, "I should be scaled up");
            Assert.True(qGain < 1.0, "Q should be scaled down");
            Assert.Equal(expectedRatio, iGain / qGain, 5);
        }

        [Fact]
        public void Dc_offsets_shift_the_mean()
        {
            var input = MakeInput();
            double dcI = 0.25;
            double dcQ = -0.15;
            var cfg = new IqImpairmentConfig { DcOffsetI = dcI, DcOffsetQ = dcQ };

            var output = IqImpairments.Apply(input, cfg);

            double inMeanI = Mean(input.I);
            double inMeanQ = Mean(input.Q);
            double outMeanI = Mean(output.I);
            double outMeanQ = Mean(output.Q);

            Assert.Equal(dcI, outMeanI - inMeanI, 5);
            Assert.Equal(dcQ, outMeanQ - inMeanQ, 5);
        }

        [Fact]
        public void Swap_iq_exchanges_channels()
        {
            var input = MakeInput();
            var cfg = new IqImpairmentConfig { SwapIq = true };

            var output = IqImpairments.Apply(input, cfg);

            for (int k = 0; k < input.Length; k++)
            {
                Assert.Equal(input.Q[k], output.I[k], 6);
                Assert.Equal(input.I[k], output.Q[k], 6);
            }
        }

        [Fact]
        public void Quadrature_skew_90deg_maps_Q_to_I()
        {
            var input = MakeInput();
            var cfg = new IqImpairmentConfig { QuadratureSkewDeg = 90.0 };

            var output = IqImpairments.Apply(input, cfg);

            // At θ = 90°: Q' = I·sin(90)+Q·cos(90) = I. I' = I (gain unchanged).
            for (int k = 0; k < input.Length; k++)
            {
                Assert.Equal(input.I[k], output.I[k], 5);
                Assert.Equal(input.I[k], output.Q[k], 5);
            }
        }
    }
}
