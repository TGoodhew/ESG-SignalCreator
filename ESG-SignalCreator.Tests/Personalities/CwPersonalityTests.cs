using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Cw;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class CwPersonalityTests
    {
        private static CwPersonality MakeWith(CwConfig cfg)
        {
            var p = new CwPersonality();
            p.LoadConfig(cfg);
            return p;
        }

        [Fact]
        public void Identity_is_stable()
        {
            var p = new CwPersonality();
            Assert.Equal("cw", p.Id);
            Assert.Equal("CW / Single tone", p.DisplayName);
            Assert.Null(p.RequiredOption);
        }

        [Fact]
        public void Envelope_is_constant_at_amplitude_scale()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 10e6,
                Length = 4000,
                FreqOffsetHz = 100e3,
                AmplitudeScale = 0.8,
                PhaseDeg = 33
            };
            WaveformModel wf = MakeWith(cfg).Calculate(null);

            Assert.True(wf.Length >= CwPersonality.MinLength);
            for (int n = 0; n < wf.Length; n++)
            {
                double mag = Math.Sqrt((double)wf.I[n] * wf.I[n] + (double)wf.Q[n] * wf.Q[n]);
                Assert.Equal(cfg.AmplitudeScale, mag, 5);
            }
        }

        [Fact]
        public void Length_holds_an_integer_number_of_cycles_for_seamless_looping()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 10e6,
                Length = 3950,        // not an exact multiple of the tone period (100 samples/cycle)
                FreqOffsetHz = 100e3  // 100 samples per cycle at 10 MHz
            };
            WaveformModel wf = MakeWith(cfg).Calculate(null);

            // The length is nudged so the tone completes a whole number of cycles.
            double cycles = cfg.FreqOffsetHz * wf.Length / cfg.SampleRateHz;
            Assert.Equal(Math.Round(cycles), cycles, 6);
            Assert.Equal(0, wf.Length % 100); // whole cycles => multiple of the 100-sample period
        }

        [Fact]
        public void Zero_offset_produces_dc()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 10e6,
                Length = 4000,
                FreqOffsetHz = 0,
                AmplitudeScale = 0.5,
                PhaseDeg = 0
            };
            WaveformModel wf = MakeWith(cfg).Calculate(null);

            Assert.Equal(cfg.Length, wf.Length); // no seamless adjustment at DC
            for (int n = 0; n < wf.Length; n++)
            {
                Assert.Equal(cfg.AmplitudeScale, wf.I[n], 5);
                Assert.Equal(0.0, wf.Q[n], 5);
            }
        }

        [Fact]
        public void Length_never_below_minimum_even_for_tiny_request()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 10e6,
                Length = 1, // absurdly small
                FreqOffsetHz = 100e3
            };
            WaveformModel wf = MakeWith(cfg).Calculate(null);
            Assert.True(wf.Length >= CwPersonality.MinLength);
        }

        [Fact]
        public void GetConfig_LoadConfig_round_trips()
        {
            var cfg = new CwConfig
            {
                SampleRateHz = 5e6,
                Length = 2048,
                FreqOffsetHz = 250e3,
                AmplitudeScale = 0.707,
                PhaseDeg = 45
            };
            var p = MakeWith(cfg);

            var got = Assert.IsType<CwConfig>(p.GetConfig());
            Assert.Equal(cfg.SampleRateHz, got.SampleRateHz);
            Assert.Equal(cfg.Length, got.Length);
            Assert.Equal(cfg.FreqOffsetHz, got.FreqOffsetHz);
            Assert.Equal(cfg.AmplitudeScale, got.AmplitudeScale);
            Assert.Equal(cfg.PhaseDeg, got.PhaseDeg);
        }

        [Fact]
        public void Calculate_reports_progress_to_completion()
        {
            int last = -1;
            var progress = new SyncProgress(v => last = v);

            MakeWith(new CwConfig { FreqOffsetHz = 100e3 }).Calculate(progress);
            Assert.Equal(100, last);
        }

        // Synchronous IProgress so the callback runs inline (no SynchronizationContext in tests).
        private sealed class SyncProgress : IProgress<int>
        {
            private readonly Action<int> _on;
            public SyncProgress(Action<int> on) { _on = on; }
            public void Report(int value) => _on(value);
        }
    }
}
