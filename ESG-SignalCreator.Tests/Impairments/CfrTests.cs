using System;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Impairments
{
    public class CfrTests
    {
        /// <summary>
        /// Build a high-PAPR signal by summing several incommensurate tones. The peaks where the
        /// tones align in phase give a crest factor well above a single CW tone, so CFR has
        /// something to reduce. Deterministic (no RNG). Peak-normalized to unit magnitude.
        /// </summary>
        private static WaveformModel MakeMultitone(int length, double sampleRateHz = 10e6)
        {
            // A handful of non-harmonically-related tones produce a bursty, high-PAPR envelope.
            double[] freqs = { 0.31e6, 0.77e6, 1.19e6, 1.63e6, 2.07e6, 2.53e6 };
            var i = new double[length];
            var q = new double[length];
            for (int n = 0; n < length; n++)
            {
                double si = 0.0, sq = 0.0;
                for (int t = 0; t < freqs.Length; t++)
                {
                    double w = 2.0 * Math.PI * freqs[t] / sampleRateHz;
                    si += Math.Cos(w * n);
                    sq += Math.Sin(w * n);
                }
                i[n] = si;
                q[n] = sq;
            }

            // Peak-normalize to unit magnitude.
            double peak = 0.0;
            for (int n = 0; n < length; n++)
            {
                double m = i[n] * i[n] + q[n] * q[n];
                if (m > peak) peak = m;
            }
            peak = Math.Sqrt(peak);
            double norm = peak > 0.0 ? 1.0 / peak : 1.0;

            var fi = new float[length];
            var fq = new float[length];
            for (int n = 0; n < length; n++)
            {
                fi[n] = (float)(i[n] * norm);
                fq[n] = (float)(q[n] * norm);
            }
            return new WaveformModel(fi, fq, sampleRateHz, "multitone");
        }

        [Fact]
        public void Reduces_papr_on_high_papr_signal()
        {
            WaveformModel input = MakeMultitone(16384);
            var cfg = new CfrConfig { TargetPaprDb = 6, Iterations = 4 };

            WaveformModel outWf = Cfr.Apply(input, cfg);

            double paprIn = Cfr.PaprDb(input);
            double paprOut = Cfr.PaprDb(outWf);

            Assert.True(paprOut < paprIn,
                $"CFR output PAPR ({paprOut:F2} dB) should be below input PAPR ({paprIn:F2} dB)");
        }

        [Fact]
        public void Reduces_papr_without_filter_too()
        {
            WaveformModel input = MakeMultitone(16384);
            var cfg = new CfrConfig { TargetPaprDb = 6, Iterations = 1, FilterAfterClip = false };

            WaveformModel outWf = Cfr.Apply(input, cfg);

            Assert.True(Cfr.PaprDb(outWf) < Cfr.PaprDb(input));
        }

        [Fact]
        public void Output_length_and_sample_rate_preserved()
        {
            WaveformModel input = MakeMultitone(8192, sampleRateHz: 12.5e6);
            var cfg = new CfrConfig();

            WaveformModel outWf = Cfr.Apply(input, cfg);

            Assert.Equal(input.Length, outWf.Length);
            Assert.Equal(input.SampleRateHz, outWf.SampleRateHz);
            Assert.Equal(input.Name, outWf.Name);
        }

        [Fact]
        public void Output_peak_is_unity()
        {
            WaveformModel input = MakeMultitone(8192);
            var cfg = new CfrConfig { TargetPaprDb = 7, Iterations = 3 };

            WaveformModel outWf = Cfr.Apply(input, cfg);

            Assert.Equal(1.0, outWf.PeakMagnitude(), 5);
        }

        [Fact]
        public void Deterministic_same_input_same_output()
        {
            WaveformModel input = MakeMultitone(4096);
            var cfg = new CfrConfig { TargetPaprDb = 7, Iterations = 4 };

            WaveformModel a = Cfr.Apply(input, cfg);
            WaveformModel b = Cfr.Apply(input, cfg);

            Assert.Equal(a.Length, b.Length);
            for (int n = 0; n < a.Length; n++)
            {
                Assert.Equal(a.I[n], b.I[n]);
                Assert.Equal(a.Q[n], b.Q[n]);
            }
        }

        [Fact]
        public void Input_is_not_mutated()
        {
            WaveformModel input = MakeMultitone(2048);
            float[] iCopy = (float[])input.I.Clone();
            float[] qCopy = (float[])input.Q.Clone();

            Cfr.Apply(input, new CfrConfig());

            Assert.Equal(iCopy, input.I);
            Assert.Equal(qCopy, input.Q);
        }

        [Fact]
        public void Zero_iterations_just_renormalizes_to_unit_peak()
        {
            WaveformModel input = MakeMultitone(2048);
            var cfg = new CfrConfig { Iterations = 0 };

            WaveformModel outWf = Cfr.Apply(input, cfg);

            // No clipping happened, so PAPR is unchanged but peak is normalized to 1.
            Assert.Equal(1.0, outWf.PeakMagnitude(), 5);
            Assert.Equal(Cfr.PaprDb(input), Cfr.PaprDb(outWf), 6);
        }
    }
}
