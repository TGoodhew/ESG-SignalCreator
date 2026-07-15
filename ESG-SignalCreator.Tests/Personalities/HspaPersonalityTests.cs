using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Hspa;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class HspaPersonalityTests
    {
        [Fact]
        public void Defaults_to_16qam_and_normalizes_to_unit_peak()
        {
            var cfg = new HspaConfig { SymbolCount = 256 };
            Assert.Equal(Modulation.QAM16, cfg.Modulation);

            WaveformModel wf = Calc(cfg);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Output_length_follows_chip_structure()
        {
            var cfg = new HspaConfig { SamplesPerChip = 4, SymbolCount = 100, SpreadingFactor = 16 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(100 * 16 * 4, wf.Length);
            Assert.Equal(3.84e6 * 4, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Qam16_is_not_constant_envelope_unlike_qpsk()
        {
            // 16QAM has amplitude variation; QPSK (spread) does not per-symbol but the RRC shaping
            // still varies. Sanity: 16QAM waveform has meaningful envelope variation.
            var cfg = new HspaConfig { SymbolCount = 256, Modulation = Modulation.QAM16 };
            WaveformModel wf = Calc(cfg);

            double min = double.MaxValue, max = 0;
            for (int s = 0; s < wf.Length; s++)
            {
                double m = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                if (m < min) min = m;
                if (m > max) max = m;
            }
            Assert.True(max - min > 0.2, "16QAM should show envelope variation");
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new HspaConfig { SymbolCount = 128 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        private static WaveformModel Calc(HspaConfig cfg)
        {
            var p = new HspaPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
