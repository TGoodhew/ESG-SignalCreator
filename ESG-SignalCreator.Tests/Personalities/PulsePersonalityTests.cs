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

        // ---- v2 (#179): additional intra-pulse modulations ----

        [Theory]
        [InlineData(IntraPulseModulation.NonLinearFmChirp)]
        [InlineData(IntraPulseModulation.FmStep)]
        [InlineData(IntraPulseModulation.AmStep)]
        [InlineData(IntraPulseModulation.Bpsk)]
        [InlineData(IntraPulseModulation.Qpsk)]
        [InlineData(IntraPulseModulation.FrankCode)]
        [InlineData(IntraPulseModulation.PolyphaseP4)]
        public void New_intra_pulse_modulations_produce_unit_peak_finite_output(IntraPulseModulation mod)
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6,
                Length = 16384,
                PulseWidthSec = 4e-6,
                PriSec = 12e-6,
                ChirpBandwidthHz = 8e6,
                Modulation = mod
            };

            WaveformModel wf = Calc(cfg);

            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
            for (int s = 0; s < wf.Length; s++)
            {
                Assert.False(float.IsNaN(wf.I[s]) || float.IsNaN(wf.Q[s]), "sample must be finite");
                Assert.False(float.IsInfinity(wf.I[s]) || float.IsInfinity(wf.Q[s]), "sample must be finite");
            }
        }

        [Fact]
        public void Phase_only_codes_have_constant_pulse_magnitude()
        {
            // BPSK/QPSK/Frank/P4 are pure phase codes: the on-pulse envelope magnitude stays 1.0.
            foreach (var mod in new[] { IntraPulseModulation.Bpsk, IntraPulseModulation.Qpsk,
                                        IntraPulseModulation.FrankCode, IntraPulseModulation.PolyphaseP4 })
            {
                var cfg = new PulseConfig
                {
                    SampleRateHz = 50e6, Length = 8192, PulseWidthSec = 4e-6, PriSec = 12e-6,
                    Modulation = mod, RiseFallSec = 0.0
                };
                WaveformModel wf = Calc(cfg);
                // Sample well inside the first pulse (200 samples = 4 us pulse is 200 samples).
                double mag = Math.Sqrt(wf.I[10] * wf.I[10] + wf.Q[10] * wf.Q[10]);
                Assert.Equal(1.0, mag, 3);
            }
        }

        [Fact]
        public void Bpsk_code_is_repeatable_by_seed_and_varies_with_seed()
        {
            PulseConfig Make(int seed) => new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 4e-6, PriSec = 12e-6,
                Modulation = IntraPulseModulation.Bpsk, PhaseCodeChips = 13, PhaseCodeSeed = seed
            };

            WaveformModel a1 = Calc(Make(7));
            WaveformModel a2 = Calc(Make(7));
            WaveformModel b = Calc(Make(8));

            for (int s = 0; s < 200; s++) Assert.Equal(a1.I[s], a2.I[s], 6); // same seed => identical
            bool anyDiff = false;
            for (int s = 0; s < 200; s++) if (Math.Abs(a1.I[s] - b.I[s]) > 1e-6) { anyDiff = true; break; }
            Assert.True(anyDiff, "a different seed should change the code");
        }

        [Fact]
        public void Frank_code_uses_n_squared_chips()
        {
            // Frank order 4 => 16 chips, phases multiples of 2*pi/4.
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 4e-6, PriSec = 12e-6,
                Modulation = IntraPulseModulation.FrankCode, FrankOrderN = 4
            };
            WaveformModel wf = Calc(cfg);
            // The first N=4 chips of a Frank code (m=0 row) are all phase 0 => Q ~ 0 there.
            Assert.Equal(0.0, wf.Q[5], 3);
        }

        // ---- v2 (#179): per-pulse offset tables (R-3) ----

        [Fact]
        public void Per_pulse_power_offset_makes_the_lower_pulse_quieter_after_normalization()
        {
            // Two pulses per period-pattern: 0 dB then -6 dB. After peak-normalization the loud pulse
            // is 1.0 and the quiet one ~0.5.
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 2e-6 /*100 samples*/, PriSec = 20e-6 /*1000*/,
                Modulation = IntraPulseModulation.None,
                PerPulsePowerOffsetsDb = new[] { 0.0, -6.0206 }  // 0.5 in linear
            };
            WaveformModel wf = Calc(cfg);

            double p0 = Math.Sqrt(wf.I[10] * wf.I[10] + wf.Q[10] * wf.Q[10]);       // first pulse
            double p1 = Math.Sqrt(wf.I[1010] * wf.I[1010] + wf.Q[1010] * wf.Q[1010]); // second pulse
            Assert.Equal(1.0, p0, 3);
            Assert.Equal(0.5, p1, 2);
        }

        [Fact]
        public void Per_pulse_phase_offset_rotates_a_pulse_into_quadrature()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 2e-6, PriSec = 20e-6,
                Modulation = IntraPulseModulation.None,
                PerPulsePhaseOffsetsDeg = new[] { 0.0, 90.0 }
            };
            WaveformModel wf = Calc(cfg);

            // First pulse: phase 0 => energy on I. Second pulse: +90 deg => energy on Q.
            Assert.True(Math.Abs(wf.I[10]) > 0.5 && Math.Abs(wf.Q[10]) < 1e-3);
            Assert.True(Math.Abs(wf.Q[1010]) > 0.5 && Math.Abs(wf.I[1010]) < 1e-3);
        }

        // ---- v2 (#179): PRI patterning (R-4) ----

        [Fact]
        public void Staggered_pri_places_pulses_at_the_pattern_intervals()
        {
            // Pattern 10us, 20us (=> 500, 1000 samples) cycling from start 0:
            // starts at 0, 500, 1500, 2000, 3000, ...
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 3600, PulseWidthSec = 1e-6, PriSec = 10e-6,
                PriMode = PriMode.Staggered, StaggerPatternSec = new[] { 10e-6, 20e-6 },
                EmitPulseMarkers = true
            };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(1, wf.Markers[0]);
            Assert.Equal(1, wf.Markers[500]);
            Assert.Equal(1, wf.Markers[1500]);
            Assert.Equal(1, wf.Markers[2000]);
            Assert.Equal(1, wf.Markers[3000]);
        }

        [Fact]
        public void Jittered_pri_is_repeatable_and_stays_within_bounds()
        {
            PulseConfig Make() => new PulseConfig
            {
                SampleRateHz = 50e6, Length = 40000, PulseWidthSec = 1e-6, PriSec = 10e-6,
                PriMode = PriMode.Jittered, PriJitterSec = 2e-6, PriJitterSeed = 42,
                EmitPulseMarkers = true
            };

            WaveformModel a = Calc(Make());
            WaveformModel b = Calc(Make());

            int[] StartsOf(WaveformModel wf)
            {
                var list = new System.Collections.Generic.List<int>();
                for (int s = 0; s < wf.Length; s++) if (wf.Markers[s] != 0) list.Add(s);
                return list.ToArray();
            }

            int[] sa = StartsOf(a), sb = StartsOf(b);
            Assert.Equal(sa, sb); // same seed => identical placement

            // Every gap is within PRI ± jitter (8..12 us => 400..600 samples), allowing 1-sample rounding.
            for (int k = 1; k < sa.Length; k++)
            {
                int gap = sa[k] - sa[k - 1];
                Assert.InRange(gap, 400 - 1, 600 + 1);
            }
        }

        [Fact]
        public void Staggered_pattern_value_below_pulse_width_is_rejected()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 5e-6, PriSec = 10e-6,
                PriMode = PriMode.Staggered, StaggerPatternSec = new[] { 10e-6, 1e-6 } // 1us < 5us pulse
            };
            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Jitter_that_could_overlap_pulses_is_rejected()
        {
            var cfg = new PulseConfig
            {
                SampleRateHz = 50e6, Length = 4096, PulseWidthSec = 9e-6, PriSec = 10e-6,
                PriMode = PriMode.Jittered, PriJitterSec = 2e-6 // 10-2=8us < 9us pulse
            };
            var p = new PulsePersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
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
