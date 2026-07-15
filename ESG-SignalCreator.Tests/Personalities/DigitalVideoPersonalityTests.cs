using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.DigitalVideo;
using EsgSignalCreator.Personalities.WimaxFixed;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class DigitalVideoPersonalityTests
    {
        [Fact]
        public void Mode8k_uses_8192_fft_and_dvbt_elementary_rate()
        {
            var cfg = new DigitalVideoConfig { Mode = DvbtMode.Mode8K, GuardInterval = CpRatio.OneEighth, SymbolCount = 2 };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(64e6 / 7.0, wf.SampleRateHz, 0); // elementary rate, independent of FFT
            int cp = 8192 / 8;
            Assert.Equal(2 * (8192 + cp), wf.Length);
        }

        [Fact]
        public void Mode2k_uses_2048_fft()
        {
            var cfg = new DigitalVideoConfig { Mode = DvbtMode.Mode2K, GuardInterval = CpRatio.OneQuarter, SymbolCount = 3 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(64e6 / 7.0, wf.SampleRateHz, 0);
            Assert.Equal(3 * (2048 + 512), wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new DigitalVideoConfig { Mode = DvbtMode.Mode2K, SymbolCount = 4 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        private static WaveformModel Calc(DigitalVideoConfig cfg)
        {
            var p = new DigitalVideoPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
