using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.WimaxFixed;
using EsgSignalCreator.Personalities.WimaxMobile;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class WimaxMobilePersonalityTests
    {
        [Fact]
        public void Fft_size_scales_the_sample_rate_at_fixed_spacing()
        {
            // 1024-FFT at 10.9375 kHz => 11.2 MHz sample rate.
            var cfg = new WimaxMobileConfig { FftSize = WimaxFftSize.Fft1024, CyclicPrefixRatio = CpRatio.OneEighth, SymbolCount = 4 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(1024 * 10.9375e3, wf.SampleRateHz, 0);
            Assert.Equal(4 * (1024 + 128), wf.Length);
        }

        [Fact]
        public void Fft128_sample_rate_is_1p4_mhz()
        {
            var cfg = new WimaxMobileConfig { FftSize = WimaxFftSize.Fft128, SymbolCount = 4 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(128 * 10.9375e3, wf.SampleRateHz, 0);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new WimaxMobileConfig { FftSize = WimaxFftSize.Fft512, SymbolCount = 8 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        private static WaveformModel Calc(WimaxMobileConfig cfg)
        {
            var p = new WimaxMobilePersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
