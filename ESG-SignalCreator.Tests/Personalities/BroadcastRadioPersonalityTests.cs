using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.BroadcastRadio;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class BroadcastRadioPersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_match_config()
        {
            var cfg = new BroadcastRadioConfig { SampleRateHz = 400e3, Length = 8000 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(8000, wf.Length);
            Assert.Equal(400e3, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Fm_is_constant_envelope()
        {
            WaveformModel wf = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = true });
            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 4);
            }
        }

        [Fact]
        public void Stereo_and_mono_differ()
        {
            var mono = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = false });
            var stereo = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = true });
            double maxDiff = 0;
            for (int s = 0; s < mono.Length; s++)
                maxDiff = Math.Max(maxDiff, Math.Abs(mono.I[s] - stereo.I[s]));
            Assert.True(maxDiff > 0.1, "stereo multiplex should change the signal");
        }

        [Fact]
        public void Low_sample_rate_for_stereo_is_rejected()
        {
            var cfg = new BroadcastRadioConfig { SampleRateHz = 60e3, Length = 8000, Stereo = true };
            var p = new BroadcastRadioPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static WaveformModel Calc(BroadcastRadioConfig cfg)
        {
            var p = new BroadcastRadioPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
