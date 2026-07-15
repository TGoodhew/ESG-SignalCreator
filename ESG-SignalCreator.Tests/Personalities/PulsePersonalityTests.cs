using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Pulse;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class PulsePersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_match_config()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 32768,
                PulseWidthSec = 1e-6,
                PriSec = 10e-6
            };

            WaveformModel wf = Calc(cfg);

            Assert.Equal(cfg.Length, wf.Length);
            Assert.Equal(cfg.SampleRateHz, wf.SampleRateHz);
        }

        [Fact]
        public void Peak_magnitude_is_normalized_to_unity()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 16384,
                PulseWidthSec = 2e-6,
                PriSec = 8e-6,
                Modulation = IntraPulseModulation.LinearFmChirp,
                ChirpBandwidthHz = 10e6
            };

            WaveformModel wf = Calc(cfg);

            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Pulse_count_and_duty_cycle_follow_pri_and_width()
        {
            // 50 MHz, 20000 samples = 400 us. PRI 10 us => a pulse start every 500 samples,
            // starting at 0: starts at 0,500,...,19500 => 40 candidate starts; pulse width 500
            // samples (10 us) means the last one (19500..20000) just fits => 40 complete pulses.
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 20000,
                PulseWidthSec = 10e-6,   // 500 samples
                PriSec = 10e-6,          // 500 samples => 100% duty (contiguous)
                Modulation = IntraPulseModulation.None
            };

            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);

            Assert.Equal(40, p.LastPulseCount);
            Assert.Equal(1.0, p.LastDutyCycle, 6);
        }

        [Fact]
        public void Off_time_between_pulses_is_silent()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 4096,
                PulseWidthSec = 1e-6,    // 50 samples on
                PriSec = 20e-6,          // 1000 samples period => 950 off
                Modulation = IntraPulseModulation.None
            };

            WaveformModel wf = Calc(cfg);

            int pulseN = 50;
            int priN = 1000;
            // A sample midway through the off region of the first period must be zero.
            int idle = pulseN + (priN - pulseN) / 2;
            double mag = Math.Sqrt(wf.I[idle] * wf.I[idle] + wf.Q[idle] * wf.Q[idle]);
            Assert.Equal(0.0, mag, 6);

            // The very first sample (pulse start) must be energised.
            double first = Math.Sqrt(wf.I[0] * wf.I[0] + wf.Q[0] * wf.Q[0]);
            Assert.True(first > 0.5, "expected the pulse to be on at sample 0");
        }

        [Fact]
        public void Unmodulated_pulse_has_zero_quadrature()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 4096,
                PulseWidthSec = 2e-6,
                PriSec = 8e-6,
                Modulation = IntraPulseModulation.None
            };

            WaveformModel wf = Calc(cfg);

            for (int s = 0; s < wf.Length; s++)
                Assert.Equal(0.0, wf.Q[s], 6);
        }

        [Fact]
        public void Markers_mark_each_pulse_start()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 3500,
                PulseWidthSec = 1e-6,
                PriSec = 20e-6,          // 1000-sample period => starts at 0,1000,2000,3000 (4000 is out of range)
                EmitPulseMarkers = true
            };

            WaveformModel wf = Calc(cfg);

            Assert.NotNull(wf.Markers);
            int markerCount = 0;
            foreach (byte b in wf.Markers) if (b != 0) markerCount++;
            Assert.Equal(4, markerCount);           // 0,1000,2000,3000
            Assert.Equal(1, wf.Markers[0]);
            Assert.Equal(1, wf.Markers[1000]);
        }

        [Fact]
        public void Barker13_phase_code_flips_at_expected_sign_changes()
        {
            int[] code = PulsePersonality.BarkerCode(13);
            Assert.Equal(13, code.Length);

            // The canonical Barker-13 (+ + + + + - - + + - + - +) has 6 internal sign changes.
            // Verify against the sequence the personality uses so the phase code stays canonical.
            int flips = 0;
            for (int k = 1; k < code.Length; k++)
                if (code[k] != code[k - 1]) flips++;
            Assert.Equal(6, flips);
        }

        [Fact]
        public void Invalid_barker_length_throws()
        {
            Assert.Throws<ArgumentException>(() => PulsePersonality.BarkerCode(6));
        }

        [Fact]
        public void Pri_shorter_than_pulse_width_is_rejected()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 4096,
                PulseWidthSec = 5e-6,
                PriSec = 1e-6            // shorter than the pulse => overlap
            };

            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Progress_reaches_100()
        {
            var cfg = new PulseConfig { SampleRateHz = 50e6, Length = 8192, PulseWidthSec = 1e-6, PriSec = 5e-6 };
            int last = -1;
            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            p.Calculate(new SyncProgress(v => last = v));
            Assert.Equal(100, last);
        }

        private static WaveformModel Calc(PulseConfig cfg)
        {
            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private sealed class SyncProgress : IProgress<int>
        {
            private readonly Action<int> _onReport;
            public SyncProgress(Action<int> onReport) { _onReport = onReport; }
            public void Report(int value) => _onReport(value);
        }
    }
}
