using System;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Impairments
{
    public class AwgnImpairmentTests
    {
        /// <summary>Build a constant-envelope CW tone (unit magnitude) of the given length.</summary>
        private static WaveformModel MakeCw(int length, double sampleRateHz = 10e6, double freqHz = 1e6)
        {
            var i = new float[length];
            var q = new float[length];
            double w = 2.0 * Math.PI * freqHz / sampleRateHz;
            for (int n = 0; n < length; n++)
            {
                i[n] = (float)Math.Cos(w * n);
                q[n] = (float)Math.Sin(w * n);
            }
            return new WaveformModel(i, q, sampleRateHz, "CW");
        }

        /// <summary>Mean power mean(I^2 + Q^2) of a residual between two equal-length waveforms.</summary>
        private static double ResidualPower(WaveformModel a, WaveformModel b)
        {
            double sum = 0.0;
            for (int n = 0; n < a.Length; n++)
            {
                double di = (double)b.I[n] - a.I[n];
                double dq = (double)b.Q[n] - a.Q[n];
                sum += di * di + dq * dq;
            }
            return sum / a.Length;
        }

        [Fact]
        public void Same_seed_yields_identical_output()
        {
            WaveformModel input = MakeCw(4096);
            var cfg = new AwgnImpairmentConfig { CarrierToNoiseDb = 15, RandomSeed = 777 };

            WaveformModel a = AwgnImpairment.Apply(input, cfg);
            WaveformModel b = AwgnImpairment.Apply(input, cfg);

            Assert.Equal(a.Length, b.Length);
            for (int n = 0; n < a.Length; n++)
            {
                Assert.Equal(a.I[n], b.I[n]);
                Assert.Equal(a.Q[n], b.Q[n]);
            }
        }

        [Fact]
        public void Higher_cn_adds_less_noise_power()
        {
            WaveformModel input = MakeCw(16384);
            // Disable renormalization so residual power directly reflects added noise.
            var lowCn = new AwgnImpairmentConfig { CarrierToNoiseDb = 10, RandomSeed = 5, RenormalizePeak = false };
            var highCn = new AwgnImpairmentConfig { CarrierToNoiseDb = 30, RandomSeed = 5, RenormalizePeak = false };

            double noiseLow = ResidualPower(input, AwgnImpairment.Apply(input, lowCn));
            double noiseHigh = ResidualPower(input, AwgnImpairment.Apply(input, highCn));

            Assert.True(noiseHigh < noiseLow,
                $"CN=30 noise ({noiseHigh:E3}) should be less than CN=10 noise ({noiseLow:E3})");
        }

        [Fact]
        public void Output_length_and_sample_rate_preserved()
        {
            WaveformModel input = MakeCw(8192, sampleRateHz: 12.5e6);
            var cfg = new AwgnImpairmentConfig { CarrierToNoiseDb = 20 };

            WaveformModel outWf = AwgnImpairment.Apply(input, cfg);

            Assert.Equal(input.Length, outWf.Length);
            Assert.Equal(input.SampleRateHz, outWf.SampleRateHz);
            Assert.Equal(input.Name, outWf.Name);
        }

        [Fact]
        public void Input_is_not_mutated()
        {
            WaveformModel input = MakeCw(2048);
            float[] iCopy = (float[])input.I.Clone();
            float[] qCopy = (float[])input.Q.Clone();

            AwgnImpairment.Apply(input, new AwgnImpairmentConfig { CarrierToNoiseDb = 12 });

            Assert.Equal(iCopy, input.I);
            Assert.Equal(qCopy, input.Q);
        }

        [Fact]
        public void Renormalize_scales_peak_to_unity()
        {
            WaveformModel input = MakeCw(8192);
            var cfg = new AwgnImpairmentConfig { CarrierToNoiseDb = 10, RenormalizePeak = true };

            WaveformModel outWf = AwgnImpairment.Apply(input, cfg);

            Assert.Equal(1.0, outWf.PeakMagnitude(), 5);
        }

        [Fact]
        public void Measured_cn_is_close_to_target_for_cw()
        {
            WaveformModel input = MakeCw(65536);
            const double target = 20.0;
            var cfg = new AwgnImpairmentConfig { CarrierToNoiseDb = target, RandomSeed = 2024, RenormalizePeak = true };

            WaveformModel outWf = AwgnImpairment.Apply(input, cfg);
            double measured = AwgnImpairment.MeasuredCnDb(input, outWf);

            Assert.True(Math.Abs(measured - target) <= 3.0,
                $"Measured C/N ({measured:F2} dB) should be within 3 dB of target ({target} dB)");
        }
    }
}
