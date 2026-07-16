using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Jitter;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class JitterPersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_match_config()
        {
            var cfg = Base();
            WaveformModel wf = Calc(cfg);
            Assert.Equal(cfg.Length, wf.Length);
            Assert.Equal(cfg.SampleRateHz, wf.SampleRateHz);
        }

        [Fact]
        public void Jitter_is_phase_only_so_envelope_stays_unit_magnitude()
        {
            // Timing/phase modulation must not change the complex envelope magnitude.
            var cfg = Base();
            cfg.PeriodicShape = JitterShape.Square;
            cfg.PeriodicUiPp = 0.9;
            cfg.RandomEnabled = true;
            cfg.RandomUiRms = 0.05;

            WaveformModel wf = Calc(cfg);

            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 4);
            }
        }

        [Fact]
        public void Zero_jitter_matches_a_clean_tone()
        {
            var cfg = Base();
            cfg.PeriodicShape = JitterShape.None;
            cfg.RandomEnabled = false;

            var p = new JitterPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);

            double fs = cfg.SampleRateHz, fclk = cfg.ClockRateHz;
            for (int s = 0; s < 64; s++)
            {
                double phase = 2.0 * Math.PI * fclk * (s / fs);
                Assert.Equal(Math.Cos(phase), wf.I[s], 4);
                Assert.Equal(Math.Sin(phase), wf.Q[s], 4);
            }
            Assert.Equal(0.0, p.LastPeakJitterSec, 12);
        }

        [Fact]
        public void Sinusoidal_jitter_changes_the_signal_and_reports_expected_peak()
        {
            var clean = Base(); clean.PeriodicShape = JitterShape.None;
            var jit = Base(); jit.PeriodicShape = JitterShape.Sinusoidal; jit.PeriodicUiPp = 0.5;

            WaveformModel wClean = Calc(clean);
            var pj = new JitterPersonality(); pj.LoadConfig(jit);
            WaveformModel wJit = pj.Calculate(null);

            double maxDiff = 0;
            for (int s = 0; s < wClean.Length; s++)
                maxDiff = Math.Max(maxDiff, Math.Abs(wClean.I[s] - wJit.I[s]));
            Assert.True(maxDiff > 0.1, "sinusoidal jitter should change the waveform");

            // Peak timing deviation = (UIpp/2) * (1/clock).
            double expectedPeak = (0.5 / 2.0) * (1.0 / jit.ClockRateHz);
            Assert.Equal(expectedPeak, pj.LastPeakJitterSec, 12);
        }

        [Fact]
        public void Random_jitter_is_repeatable_for_the_same_seed()
        {
            var cfg = Base();
            cfg.PeriodicShape = JitterShape.None;
            cfg.RandomEnabled = true;
            cfg.RandomUiRms = 0.03;
            cfg.RandomSeed = 777;

            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++)
            {
                Assert.Equal(a.I[s], b.I[s], 7);
                Assert.Equal(a.Q[s], b.Q[s], 7);
            }

            // A different seed should generally differ.
            var cfg2 = Base();
            cfg2.PeriodicShape = JitterShape.None;
            cfg2.RandomEnabled = true;
            cfg2.RandomUiRms = 0.03;
            cfg2.RandomSeed = 778;
            WaveformModel c = Calc(cfg2);

            double maxDiff = 0;
            for (int s = 0; s < a.Length; s++) maxDiff = Math.Max(maxDiff, Math.Abs(a.I[s] - c.I[s]));
            Assert.True(maxDiff > 0.0, "different seeds should produce different jitter");
        }

        [Fact]
        public void Nonpositive_clock_rate_is_rejected()
        {
            var cfg = Base(); cfg.ClockRateHz = 0;
            var p = new JitterPersonality(); p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        // ---- v2 (#182): custom shape, sweep, masks, range enforcement ----

        [Fact]
        public void Custom_shape_is_used_and_stays_phase_only()
        {
            // A custom one-period profile: a simple ramp table. Envelope must remain unit-magnitude.
            var cfg = Base();
            cfg.PeriodicShape = JitterShape.Custom;
            cfg.PeriodicUiPp = 0.4;
            cfg.CustomShapeSamples = new[] { -1.0, -0.5, 0.0, 0.5, 1.0, 0.5, 0.0, -0.5 };

            var p = new JitterPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);

            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 4);
            }
            // Peak displacement = (UIpp/2)*(1/clock) since the table reaches +/-1.
            Assert.Equal((0.4 / 2.0) / cfg.ClockRateHz, p.LastPeakJitterSec, 12);
        }

        [Fact]
        public void Empty_custom_shape_behaves_as_no_jitter()
        {
            var cfg = Base();
            cfg.PeriodicShape = JitterShape.Custom;
            cfg.CustomShapeSamples = null;

            var p = new JitterPersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);
            Assert.Equal(0.0, p.LastPeakJitterSec, 12);
        }

        [Fact]
        public void Sweep_changes_the_waveform_and_is_repeatable()
        {
            var cfg = Base();
            cfg.Length = 16384;
            cfg.PeriodicShape = JitterShape.Sinusoidal;
            cfg.PeriodicUiPp = 0.5;
            cfg.SweepEnabled = true;
            cfg.SweepStartHz = 10e3;
            cfg.SweepStopHz = 1e6;
            cfg.SweepMode = JitterSweepMode.Logarithmic;

            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s += 101) Assert.Equal(a.I[s], b.I[s], 6); // deterministic

            var clean = Base(); clean.Length = 16384; clean.PeriodicShape = JitterShape.None;
            WaveformModel wc = Calc(clean);
            double maxDiff = 0;
            for (int s = 0; s < a.Length; s++) maxDiff = Math.Max(maxDiff, Math.Abs(a.I[s] - wc.I[s]));
            Assert.True(maxDiff > 0.1, "the sweep should modulate the tone");
        }

        [Fact]
        public void Sweep_following_a_custom_mask_scales_amplitude_with_frequency()
        {
            // Custom mask: 1.0 UIpp at 1 kHz down to 0.01 UIpp at 1 MHz. Sweeping start->stop should
            // give a larger peak deviation near the start (high UI) than a high-freq-only sweep.
            JitterConfig Make(double startHz, double stopHz) => new JitterConfig
            {
                SampleRateHz = 100e6, Length = 16384, ClockRateHz = 10e6,
                PeriodicShape = JitterShape.Sinusoidal,
                SweepEnabled = true, SweepStartHz = startHz, SweepStopHz = stopHz,
                SweepMode = JitterSweepMode.Logarithmic, SweepFollowMask = true,
                MaskStandard = JitterMask.Custom,
                CustomMaskFreqHz = new[] { 1e3, 1e6 },
                CustomMaskUiPp = new[] { 1.0, 0.01 }
            };

            var lowBand = new JitterPersonality(); lowBand.LoadConfig(Make(1e3, 3e3));
            lowBand.Calculate(null);
            var highBand = new JitterPersonality(); highBand.LoadConfig(Make(300e3, 1e6));
            highBand.Calculate(null);

            Assert.True(lowBand.LastPeakJitterSec > highBand.LastPeakJitterSec * 5,
                "low-frequency sweep should see much larger mask amplitude");
        }

        [Fact]
        public void G8251_mask_has_the_expected_shape()
        {
            // Flat plateau at low freq, 0.15 UIpp floor at high freq, monotonic roll-off between.
            double lo = JitterMasks.AmplitudeUiPp(JitterMask.G8251Oc192, 100, null, null);
            double mid = JitterMasks.AmplitudeUiPp(JitterMask.G8251Oc192, 260_000, null, null);
            double hi = JitterMasks.AmplitudeUiPp(JitterMask.G8251Oc192, 20e6, null, null);

            Assert.Equal(15.0, lo, 6);
            Assert.Equal(0.15, hi, 6);
            Assert.True(mid < lo && mid > hi, "the roll-off amplitude must sit between the plateau and the floor");
        }

        [Fact]
        public void Clock_at_or_above_nyquist_is_rejected()
        {
            var cfg = Base(); cfg.SampleRateHz = 10e6; cfg.ClockRateHz = 5e6; // exactly Nyquist
            var p = new JitterPersonality(); p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Jitter_rate_above_nyquist_is_rejected()
        {
            var cfg = Base(); cfg.SampleRateHz = 100e6; cfg.PeriodicRateHz = 60e6;
            var p = new JitterPersonality(); p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Amplitude_above_the_max_cap_is_rejected()
        {
            var cfg = Base(); cfg.PeriodicUiPp = 0.5; cfg.MaxJitterUiPp = 0.2;
            var p = new JitterPersonality(); p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static JitterConfig Base() => new JitterConfig
        {
            SampleRateHz = 100e6,
            Length = 4096,
            ClockRateHz = 10e6,
            PeriodicRateHz = 100e3,
            PeriodicUiPp = 0.2
        };

        private static WaveformModel Calc(JitterConfig cfg)
        {
            var p = new JitterPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
