using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Wcdma;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class WcdmaPersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_follow_chip_structure()
        {
            var cfg = new WcdmaConfig
            {
                ChipRateHz = 3.84e6,
                SamplesPerChip = 4,
                SymbolCount = 64,
                SpreadingFactor = 16
            };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(64 * 16 * 4, wf.Length);       // symbols × SF × samples/chip
            Assert.Equal(3.84e6 * 4, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Peak_magnitude_is_normalized_to_unity()
        {
            WaveformModel wf = Calc(new WcdmaConfig { SymbolCount = 128 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new WcdmaConfig { SymbolCount = 64, ScrambleSeed = 5 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        [Fact]
        public void Non_power_of_two_spreading_factor_is_rejected()
        {
            var cfg = new WcdmaConfig { SpreadingFactor = 12 };
            var p = new WcdmaPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Ovsf_codes_are_orthogonal()
        {
            int sf = 8;
            for (int a = 0; a < sf; a++)
                for (int b = a + 1; b < sf; b++)
                {
                    int[] ca = DsssEngine.OvsfCode(sf, a);
                    int[] cb = DsssEngine.OvsfCode(sf, b);
                    int dot = 0;
                    for (int c = 0; c < sf; c++) dot += ca[c] * cb[c];
                    Assert.Equal(0, dot);
                }
        }

        [Fact]
        public void Ovsf_row_zero_is_all_ones()
        {
            int[] c = DsssEngine.OvsfCode(16, 0);
            foreach (int v in c) Assert.Equal(1, v);
        }

        // ---- v2 (#183): multi-code composite ----

        [Fact]
        public void Multi_code_keeps_the_chip_structure_and_is_unit_peak()
        {
            var cfg = new WcdmaConfig { SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(64 * 16 * 4, wf.Length);           // multi-code doesn't change the chip layout
            Assert.Equal(3.84e6 * 4, wf.SampleRateHz, 3);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
            for (int s = 0; s < wf.Length; s++)
                Assert.False(float.IsNaN(wf.I[s]) || float.IsNaN(wf.Q[s]));
        }

        [Fact]
        public void Multi_code_differs_from_single_code()
        {
            var single = new WcdmaConfig { SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 1 };
            var multi = new WcdmaConfig { SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4 };
            WaveformModel a = Calc(single), b = Calc(multi);
            double maxDiff = 0;
            for (int s = 0; s < a.Length; s++) maxDiff = Math.Max(maxDiff, Math.Abs(a.I[s] - b.I[s]));
            Assert.True(maxDiff > 0.05, "the multi-code composite must differ from the single code");
        }

        [Fact]
        public void Multi_code_raises_papr()
        {
            // Both waveforms are peak-normalized to 1.0, so a lower average power means a higher PAPR.
            // Summing several orthogonal codes raises the peak-to-average ratio.
            double single = AvgPower(Calc(new WcdmaConfig { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 1 }));
            double multi = AvgPower(Calc(new WcdmaConfig { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 8 }));
            Assert.True(multi < single, $"multi-code avg power ({multi:F4}) should be below single-code ({single:F4})");
        }

        [Fact]
        public void Code_channel_count_above_spreading_factor_is_rejected()
        {
            var cfg = new WcdmaConfig { SpreadingFactor = 4, CodeChannelCount = 8 };
            var p = new WcdmaPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static double AvgPower(WaveformModel wf)
        {
            double sum = 0;
            for (int s = 0; s < wf.Length; s++) sum += (double)wf.I[s] * wf.I[s] + (double)wf.Q[s] * wf.Q[s];
            return sum / wf.Length;
        }

        private static WaveformModel Calc(WcdmaConfig cfg)
        {
            var p = new WcdmaPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
