using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Tdmb;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class TdmbPersonalityTests
    {
        [Theory]
        [InlineData(DabMode.ModeI, 2048, 504)]
        [InlineData(DabMode.ModeII, 512, 126)]
        [InlineData(DabMode.ModeIII, 256, 63)]
        [InlineData(DabMode.ModeIV, 1024, 252)]
        public void Mode_sets_fft_and_guard_at_2p048_mhz(DabMode mode, int fft, int guard)
        {
            var cfg = new TdmbConfig { Mode = mode, SymbolCount = 4 };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(2.048e6, wf.SampleRateHz, 0); // all modes keep 2.048 MHz
            Assert.Equal(4 * (fft + guard), wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new TdmbConfig { Mode = DabMode.ModeII, SymbolCount = 8 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        private static WaveformModel Calc(TdmbConfig cfg)
        {
            var p = new TdmbPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
