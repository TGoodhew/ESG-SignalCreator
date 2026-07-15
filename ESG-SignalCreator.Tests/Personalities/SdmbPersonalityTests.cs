using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Sdmb;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class SdmbPersonalityTests
    {
        [Fact]
        public void Produces_normalized_waveform_of_expected_length()
        {
            var cfg = new SdmbConfig { SamplesPerChip = 4, SymbolCount = 64, SpreadingFactor = 16 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(64 * 16 * 4, wf.Length);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new SdmbConfig { SymbolCount = 64 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        private static WaveformModel Calc(SdmbConfig cfg)
        {
            var p = new SdmbPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
