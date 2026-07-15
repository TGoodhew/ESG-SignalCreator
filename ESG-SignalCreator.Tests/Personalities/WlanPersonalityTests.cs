using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Wlan;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class WlanPersonalityTests
    {
        [Fact]
        public void Twenty_mhz_uses_64_fft_and_20_mhz_sample_rate()
        {
            var cfg = new WlanConfig { Bandwidth = WlanBandwidth.Bw20MHz, SymbolCount = 10 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(20e6, wf.SampleRateHz, 0);      // 64 × 312.5 kHz
            Assert.Equal(10 * (64 + 16), wf.Length);
        }

        [Fact]
        public void Forty_mhz_uses_128_fft_and_40_mhz_sample_rate()
        {
            var cfg = new WlanConfig { Bandwidth = WlanBandwidth.Bw40MHz, SymbolCount = 4 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(40e6, wf.SampleRateHz, 0);
            Assert.Equal(4 * (128 + 32), wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new WlanConfig { SymbolCount = 16 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        private static WaveformModel Calc(WlanConfig cfg)
        {
            var p = new WlanPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
