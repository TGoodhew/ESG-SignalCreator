using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.WimaxFixed;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class WimaxFixedPersonalityTests
    {
        [Fact]
        public void Sample_rate_uses_8_over_7_factor_and_256_fft()
        {
            var cfg = new WimaxFixedConfig
            {
                ChannelBandwidthHz = 3.5e6,
                CyclicPrefixRatio = CpRatio.OneEighth,
                SymbolCount = 8
            };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(3.5e6 * 8.0 / 7.0, wf.SampleRateHz, 0);
            int cp = 256 / 8;
            Assert.Equal(8 * (256 + cp), wf.Length);
        }

        [Fact]
        public void Cp_ratio_changes_symbol_length()
        {
            var q = Calc(new WimaxFixedConfig { CyclicPrefixRatio = CpRatio.OneQuarter, SymbolCount = 4 });
            var t = Calc(new WimaxFixedConfig { CyclicPrefixRatio = CpRatio.OneThirtySecond, SymbolCount = 4 });
            Assert.Equal(4 * (256 + 64), q.Length);
            Assert.Equal(4 * (256 + 8), t.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new WimaxFixedConfig { SymbolCount = 16 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        private static WaveformModel Calc(WimaxFixedConfig cfg)
        {
            var p = new WimaxFixedPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
