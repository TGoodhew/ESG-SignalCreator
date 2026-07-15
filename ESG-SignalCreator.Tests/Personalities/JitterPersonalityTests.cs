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
