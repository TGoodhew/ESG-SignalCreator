using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Lte;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class LteTddPersonalityTests
    {
        [Fact]
        public void Uses_same_ofdm_numerology_as_fdd()
        {
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw10MHz, SymbolCount = 7 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(15.36e6, wf.SampleRateHz, 0);
            Assert.Equal(7 * (1024 + 72), wf.Length);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Rejects_wrong_config_type()
        {
            var p = new LteTddPersonality();
            Assert.Throws<System.ArgumentException>(() => p.LoadConfig("not a config"));
        }

        private static WaveformModel Calc(LteConfig cfg)
        {
            var p = new LteTddPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
