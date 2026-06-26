using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Multitone;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class MultitonePersonalityTests
    {
        [Fact]
        public void AutoSpacing_of_8_tones_yields_8_enabled_tones_and_matching_length_and_finite_papr()
        {
            var cfg = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 8192,
                Phase = PhaseStrategy.Newman,
                Tones = MultitonePersonality.AutoSpacing(8, 100e3, 0.0, -6.0)
            };

            Assert.Equal(8, cfg.Tones.Length);
            foreach (var t in cfg.Tones) Assert.True(t.Enabled);

            var p = new MultitonePersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);

            Assert.Equal(cfg.Length, wf.Length);
            Assert.False(double.IsNaN(p.LastPaprDb) || double.IsInfinity(p.LastPaprDb), "PAPR should be finite");
            Assert.True(p.LastPaprDb >= 0.0);
        }

        [Fact]
        public void Newman_papr_is_strictly_below_equal_papr_for_16_equal_amplitude_tones()
        {
            Tone[] MakeTones() => MultitonePersonality.AutoSpacing(16, 50e3, 0.0, 0.0);

            double newman = PaprFor(PhaseStrategy.Newman, MakeTones());
            double equal = PaprFor(PhaseStrategy.Equal, MakeTones());

            Assert.True(newman < equal,
                $"Expected Newman PAPR ({newman:F3} dB) < Equal PAPR ({equal:F3} dB)");
        }

        [Fact]
        public void Disabled_tone_is_excluded_from_the_sum()
        {
            // Two tones at distinct offsets; disabling one must change the waveform.
            var both = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 4096,
                Phase = PhaseStrategy.Equal,
                Tones = new[]
                {
                    new Tone { FreqOffsetHz = 100e3, PowerDb = 0, Enabled = true },
                    new Tone { FreqOffsetHz = 250e3, PowerDb = 0, Enabled = true }
                }
            };

            var oneDisabled = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 4096,
                Phase = PhaseStrategy.Equal,
                Tones = new[]
                {
                    new Tone { FreqOffsetHz = 100e3, PowerDb = 0, Enabled = true },
                    new Tone { FreqOffsetHz = 250e3, PowerDb = 0, Enabled = false }
                }
            };

            WaveformModel wBoth = Calc(both);
            WaveformModel wOne = Calc(oneDisabled);

            // With one tone disabled, only a single tone remains => constant unit envelope.
            for (int s = 0; s < wOne.Length; s++)
            {
                double mag = Math.Sqrt(wOne.I[s] * wOne.I[s] + wOne.Q[s] * wOne.Q[s]);
                Assert.Equal(1.0, mag, 5);
            }

            // The two-tone waveform must differ materially from the single-tone one.
            double maxDiff = 0.0;
            for (int s = 0; s < wOne.Length; s++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(wBoth.I[s] - wOne.I[s]));
                maxDiff = Math.Max(maxDiff, Math.Abs(wBoth.Q[s] - wOne.Q[s]));
            }
            Assert.True(maxDiff > 0.1, "Disabling a tone should change the waveform");
        }

        [Fact]
        public void Single_enabled_tone_has_papr_near_zero_db()
        {
            var cfg = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 8192,
                Phase = PhaseStrategy.Newman,
                Tones = new[]
                {
                    new Tone { FreqOffsetHz = 123e3, PowerDb = 0, Enabled = true }
                }
            };

            var p = new MultitonePersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);

            Assert.Equal(0.0, p.LastPaprDb, 1); // ~0.1 dB tolerance (1 decimal place)
        }

        private static double PaprFor(PhaseStrategy strategy, Tone[] tones)
        {
            var cfg = new MultitoneConfig
            {
                SampleRateHz = 10e6,
                Length = 16384,
                Phase = strategy,
                Tones = tones
            };
            var p = new MultitonePersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);
            return p.LastPaprDb;
        }

        private static WaveformModel Calc(MultitoneConfig cfg)
        {
            var p = new MultitonePersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
