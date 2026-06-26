using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.MultiCarrier;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class MultiCarrierPersonalityTests
    {
        [Fact]
        public void Sum_of_evenly_spaced_carriers_has_configured_length_and_finite_values()
        {
            var cfg = new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = 8192,
                Carriers = MultiCarrierPersonality.EvenlySpaced(5, 200e3, -6.0)
            };

            Assert.Equal(5, cfg.Carriers.Length);

            WaveformModel wf = Calc(cfg);

            Assert.Equal(cfg.Length, wf.Length);
            for (int s = 0; s < wf.Length; s++)
            {
                Assert.True(IsFinite(wf.I[s]) && IsFinite(wf.Q[s]),
                    $"Sample {s} must be finite");
            }
        }

        [Fact]
        public void Disabled_carrier_is_excluded_from_the_sum()
        {
            var both = new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = 4096,
                Carriers = new[]
                {
                    new Carrier { FreqOffsetHz = 100e3, PowerDb = 0, Enabled = true },
                    new Carrier { FreqOffsetHz = 250e3, PowerDb = 0, Enabled = true }
                }
            };

            var oneDisabled = new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = 4096,
                Carriers = new[]
                {
                    new Carrier { FreqOffsetHz = 100e3, PowerDb = 0, Enabled = true },
                    new Carrier { FreqOffsetHz = 250e3, PowerDb = 0, Enabled = false }
                }
            };

            WaveformModel wBoth = Calc(both);
            WaveformModel wOne = Calc(oneDisabled);

            // With one carrier disabled, a single carrier remains => constant unit envelope.
            for (int s = 0; s < wOne.Length; s++)
            {
                double mag = Math.Sqrt(wOne.I[s] * wOne.I[s] + wOne.Q[s] * wOne.Q[s]);
                Assert.Equal(1.0, mag, 5);
            }

            // The two-carrier waveform must differ materially from the single-carrier one.
            double maxDiff = 0.0;
            for (int s = 0; s < wOne.Length; s++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(wBoth.I[s] - wOne.I[s]));
                maxDiff = Math.Max(maxDiff, Math.Abs(wBoth.Q[s] - wOne.Q[s]));
            }
            Assert.True(maxDiff > 0.1, "Disabling a carrier should change the waveform");
        }

        [Fact]
        public void DelaySamples_shifts_a_single_carrier_circularly()
        {
            const int length = 4096;
            const int delay = 37;

            // A non-zero offset so the delay produces an observable change. Phase 0, single carrier
            // => after normalization the envelope is unit and the samples are a pure exponential.
            Carrier MakeCarrier(int d) => new Carrier
            {
                FreqOffsetHz = 300e3,
                PowerDb = 0,
                PhaseDeg = 0,
                DelaySamples = d,
                Enabled = true
            };

            var noDelay = Calc(new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = length,
                Carriers = new[] { MakeCarrier(0) }
            });

            var delayed = Calc(new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = length,
                Carriers = new[] { MakeCarrier(delay) }
            });

            // The delayed waveform sample s must equal the non-delayed waveform at (s - delay) mod n.
            for (int s = 0; s < length; s++)
            {
                int src = ((s - delay) % length + length) % length;
                Assert.Equal(noDelay.I[src], delayed.I[s], 5);
                Assert.Equal(noDelay.Q[src], delayed.Q[s], 5);
            }

            // Sanity: the delay actually changes the waveform (sequences are not identical).
            double maxDiff = 0.0;
            for (int s = 0; s < length; s++)
            {
                maxDiff = Math.Max(maxDiff, Math.Abs(noDelay.I[s] - delayed.I[s]));
                maxDiff = Math.Max(maxDiff, Math.Abs(noDelay.Q[s] - delayed.Q[s]));
            }
            Assert.True(maxDiff > 0.1, "A non-zero delay should change the waveform");
        }

        [Fact]
        public void Single_carrier_at_zero_offset_has_constant_envelope()
        {
            var cfg = new MultiCarrierConfig
            {
                SampleRateHz = 10e6,
                Length = 4096,
                Carriers = new[]
                {
                    new Carrier { FreqOffsetHz = 0, PowerDb = 0, PhaseDeg = 30, Enabled = true }
                }
            };

            WaveformModel wf = Calc(cfg);

            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 5);
            }
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static WaveformModel Calc(MultiCarrierConfig cfg)
        {
            var p = new MultiCarrierPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
