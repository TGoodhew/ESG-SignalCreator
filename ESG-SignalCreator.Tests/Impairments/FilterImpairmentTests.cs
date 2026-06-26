using System;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Impairments
{
    public class FilterImpairmentTests
    {
        private const double SampleRateHz = 20e6;

        /// <summary>Build a complex CW tone (unit magnitude) at <paramref name="freqHz"/>.</summary>
        private static WaveformModel MakeCw(int length, double freqHz,
            double sampleRateHz = SampleRateHz, string name = "CW")
        {
            var i = new float[length];
            var q = new float[length];
            double w = 2.0 * Math.PI * freqHz / sampleRateHz;
            for (int n = 0; n < length; n++)
            {
                i[n] = (float)Math.Cos(w * n);
                q[n] = (float)Math.Sin(w * n);
            }
            return new WaveformModel(i, q, sampleRateHz, name);
        }

        /// <summary>RMS of the vector magnitude over the steady-state interior of the waveform.</summary>
        private static double InteriorRms(WaveformModel wf)
        {
            // Skip the filter's transient edges (group delay) at both ends.
            int guard = wf.Length / 8;
            double sum = 0.0;
            int count = 0;
            for (int n = guard; n < wf.Length - guard; n++)
            {
                sum += (double)wf.I[n] * wf.I[n] + (double)wf.Q[n] * wf.Q[n];
                count++;
            }
            return Math.Sqrt(sum / count);
        }

        [Fact]
        public void LowPass_taps_are_symmetric_and_sum_to_unity()
        {
            var cfg = new FilterConfig { Type = FilterType.LowPass, CutoffHz = 3e6, Taps = 65 };
            double[] taps = FilterImpairment.DesignTaps(cfg, SampleRateHz);

            Assert.Equal(65, taps.Length); // already odd
            Assert.True(taps.Length % 2 == 1);

            // Symmetric about the centre tap (linear phase).
            for (int k = 0; k < taps.Length / 2; k++)
                Assert.Equal(taps[k], taps[taps.Length - 1 - k], 10);

            // Unit DC gain.
            double sum = 0.0;
            foreach (double t in taps) sum += t;
            Assert.Equal(1.0, sum, 9);
        }

        [Fact]
        public void Even_tap_count_is_forced_odd()
        {
            var cfg = new FilterConfig { Type = FilterType.LowPass, CutoffHz = 2e6, Taps = 64 };
            double[] taps = FilterImpairment.DesignTaps(cfg, SampleRateHz);
            Assert.Equal(65, taps.Length);
        }

        [Fact]
        public void LowPass_attenuates_high_tone_more_than_low_tone()
        {
            const int len = 8192;
            // Cutoff at 2 MHz: 0.5 MHz passes, 6 MHz is well into the stop band.
            var cfg = new FilterConfig { Type = FilterType.LowPass, CutoffHz = 2e6, Taps = 65 };

            WaveformModel low = MakeCw(len, 0.5e6);
            WaveformModel high = MakeCw(len, 6e6);

            double lowOut = InteriorRms(FilterImpairment.Apply(low, cfg));
            double highOut = InteriorRms(FilterImpairment.Apply(high, cfg));

            // Both inputs have unit-magnitude (RMS 1.0). The low tone should pass ~unchanged;
            // the high tone should be strongly attenuated.
            Assert.True(lowOut > 0.9, $"low tone should pass (RMS {lowOut:F3})");
            Assert.True(highOut < 0.1, $"high tone should be attenuated (RMS {highOut:F3})");
            Assert.True(highOut < lowOut);
        }

        [Fact]
        public void HighPass_attenuates_low_tone_more_than_high_tone()
        {
            const int len = 8192;
            var cfg = new FilterConfig { Type = FilterType.HighPass, CutoffHz = 2e6, Taps = 65 };

            WaveformModel low = MakeCw(len, 0.5e6);
            WaveformModel high = MakeCw(len, 6e6);

            double lowOut = InteriorRms(FilterImpairment.Apply(low, cfg));
            double highOut = InteriorRms(FilterImpairment.Apply(high, cfg));

            // Opposite of the low-pass: the high tone passes, the low tone is attenuated.
            Assert.True(highOut > lowOut,
                $"high tone ({highOut:F3}) should pass more than low tone ({lowOut:F3})");
            Assert.True(lowOut < 0.5, $"low tone should be attenuated (RMS {lowOut:F3})");
        }

        [Fact]
        public void BandPass_passes_center_attenuates_out_of_band()
        {
            const int len = 8192;
            // Pass band 4 MHz ± 0.5 MHz => [3.5, 4.5] MHz.
            var cfg = new FilterConfig
            {
                Type = FilterType.BandPass,
                CutoffHz = 4e6,
                BandwidthHz = 1e6,
                Taps = 129
            };

            WaveformModel center = MakeCw(len, 4e6);
            WaveformModel outOfBand = MakeCw(len, 0.5e6);

            double centerOut = InteriorRms(FilterImpairment.Apply(center, cfg));
            double outOut = InteriorRms(FilterImpairment.Apply(outOfBand, cfg));

            Assert.True(centerOut > outOut,
                $"centre tone ({centerOut:F3}) should pass more than out-of-band ({outOut:F3})");
            Assert.True(outOut < 0.2, $"out-of-band tone should be attenuated (RMS {outOut:F3})");
        }

        [Fact]
        public void Output_length_and_sample_rate_preserved()
        {
            WaveformModel input = MakeCw(4096, 1e6, sampleRateHz: 12.5e6, name: "src");
            var cfg = new FilterConfig { Type = FilterType.LowPass, CutoffHz = 2e6 };

            WaveformModel outWf = FilterImpairment.Apply(input, cfg);

            Assert.Equal(input.Length, outWf.Length);
            Assert.Equal(input.SampleRateHz, outWf.SampleRateHz);
            Assert.Equal(input.Name, outWf.Name);
        }

        [Fact]
        public void Input_is_not_mutated()
        {
            WaveformModel input = MakeCw(2048, 1e6);
            float[] iCopy = (float[])input.I.Clone();
            float[] qCopy = (float[])input.Q.Clone();

            FilterImpairment.Apply(input, new FilterConfig { Type = FilterType.LowPass, CutoffHz = 2e6 });

            Assert.Equal(iCopy, input.I);
            Assert.Equal(qCopy, input.Q);
        }
    }
}
