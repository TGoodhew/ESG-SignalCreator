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

        private static WaveformModel Calc(TdScdmaConfig cfg)
        {
            var p = new TdScdmaPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
