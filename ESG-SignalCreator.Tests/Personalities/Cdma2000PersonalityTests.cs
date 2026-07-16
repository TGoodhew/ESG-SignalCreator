using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Cdma2000;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class Cdma2000PersonalityTests
    {
        [Fact]
        public void Sample_rate_follows_1p2288_mcps_chip_rate()
        {
            var cfg = new Cdma2000Config { SamplesPerChip = 4, SymbolCount = 64, SpreadingFactor = 16 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(1.2288e6 * 4, wf.SampleRateHz, 3);
            Assert.Equal(64 * 16 * 4, wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new Cdma2000Config { SymbolCount = 128 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new Cdma2000Config { SymbolCount = 64 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        // ---- v2 (#185): multi-code (Walsh code channels) ----

        [Fact]
        public void Multi_code_differs_and_raises_papr()
        {
            var single = new Cdma2000Config { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 1 };
            var multi = new Cdma2000Config { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 6 };
            WaveformModel a = Calc(single), b = Calc(multi);
            Assert.Equal(a.Length, b.Length);
            Assert.Equal(1.0, b.PeakMagnitude(), 4);
            double maxDiff = 0;
            for (int s = 0; s < a.Length; s++) maxDiff = Math.Max(maxDiff, Math.Abs(a.I[s] - b.I[s]));
            Assert.True(maxDiff > 0.05, "multi-code must differ from single-code");
            Assert.True(AvgPower(b) < AvgPower(a), "multi-code raises PAPR");
        }

        [Fact]
        public void Code_channel_count_above_spreading_factor_is_rejected()
        {
            var cfg = new Cdma2000Config { SpreadingFactor = 4, CodeChannelCount = 8 };
            var p = new Cdma2000Personality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static double AvgPower(WaveformModel wf)
        {
            double sum = 0;
            for (int s = 0; s < wf.Length; s++) sum += (double)wf.I[s] * wf.I[s] + (double)wf.Q[s] * wf.Q[s];
            return sum / wf.Length;
        }

        private static WaveformModel Calc(Cdma2000Config cfg)
        {
            var p = new Cdma2000Personality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
