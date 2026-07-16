using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.TdScdma;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class TdScdmaPersonalityTests
    {
        [Fact]
        public void Sample_rate_follows_1p28_mcps_chip_rate()
        {
            var cfg = new TdScdmaConfig { SamplesPerChip = 8, SymbolCount = 32, SpreadingFactor = 16 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(1.28e6 * 8, wf.SampleRateHz, 3);
            Assert.Equal(32 * 16 * 8, wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new TdScdmaConfig { SymbolCount = 128 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new TdScdmaConfig { SymbolCount = 64 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        // ---- v2 (#187): multi-code within a timeslot ----

        [Fact]
        public void Multi_code_differs_and_raises_papr()
        {
            var single = new TdScdmaConfig { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 1 };
            var multi = new TdScdmaConfig { SymbolCount = 128, SpreadingFactor = 16, CodeChannelCount = 6 };
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
            var cfg = new TdScdmaConfig { SpreadingFactor = 4, CodeChannelCount = 8 };
            var p = new TdScdmaPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static double AvgPower(WaveformModel wf)
        {
            double sum = 0;
            for (int s = 0; s < wf.Length; s++) sum += (double)wf.I[s] * wf.I[s] + (double)wf.Q[s] * wf.Q[s];
            return sum / wf.Length;
        }

        private static WaveformModel Calc(TdScdmaConfig cfg)
        {
            var p = new TdScdmaPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
