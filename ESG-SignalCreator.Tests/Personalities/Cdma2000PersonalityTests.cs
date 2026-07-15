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

        private static WaveformModel Calc(Cdma2000Config cfg)
        {
            var p = new Cdma2000Personality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
