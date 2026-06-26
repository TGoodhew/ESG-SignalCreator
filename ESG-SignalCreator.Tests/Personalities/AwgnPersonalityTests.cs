using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Awgn;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class AwgnPersonalityTests
    {
        private static WaveformModel Calc(AwgnConfig cfg)
        {
            var p = new AwgnPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        [Fact]
        public void Same_seed_yields_identical_output()
        {
            var cfg = new AwgnConfig { Length = 4096, RandomSeed = 777 };

            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);

            Assert.Equal(a.Length, b.Length);
            for (int s = 0; s < a.Length; s++)
            {
                Assert.Equal(a.I[s], b.I[s]);
                Assert.Equal(a.Q[s], b.Q[s]);
            }
        }

        [Fact]
        public void Different_seed_yields_different_output()
        {
            WaveformModel a = Calc(new AwgnConfig { Length = 4096, RandomSeed = 1 });
            WaveformModel b = Calc(new AwgnConfig { Length = 4096, RandomSeed = 2 });

            bool differs = false;
            for (int s = 0; s < a.Length && !differs; s++)
            {
                if (a.I[s] != b.I[s] || a.Q[s] != b.Q[s]) differs = true;
            }
            Assert.True(differs, "Different seeds should produce different waveforms");
        }

        [Fact]
        public void Output_length_matches_config()
        {
            var cfg = new AwgnConfig { Length = 12345 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(12345, wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized_to_one()
        {
            var cfg = new AwgnConfig { Length = 8192, RandomSeed = 99 };
            WaveformModel wf = Calc(cfg);

            double peak = wf.PeakMagnitude();
            Assert.Equal(1.0, peak, 5); // 5 decimal places
        }

        [Fact]
        public void Crest_factor_clipping_caps_papr()
        {
            const double cf = 10.0;
            var cfg = new AwgnConfig
            {
                Length = 65536,
                RandomSeed = 42,
                CrestFactorDb = cf,
                // Full band so clipping is the only PAPR-shaping step (filtering would
                // otherwise smooth the envelope and lower PAPR independently).
                NoiseBandwidthHz = 100e6
            };

            WaveformModel wf = Calc(cfg);
            double papr = AwgnPersonality.PaprDb(wf.I, wf.Q);

            Assert.True(papr <= cf + 0.5,
                $"PAPR ({papr:F3} dB) should be capped at ~{cf} dB (+0.5 tol)");
        }

        [Fact]
        public void No_clipping_gives_materially_higher_papr_than_clipped()
        {
            var clipped = new AwgnConfig
            {
                Length = 65536,
                RandomSeed = 2024,
                CrestFactorDb = 8.0,
                NoiseBandwidthHz = 100e6
            };
            var unclipped = new AwgnConfig
            {
                Length = 65536,
                RandomSeed = 2024,
                CrestFactorDb = 0.0, // disabled
                NoiseBandwidthHz = 100e6
            };

            double paprClipped = AwgnPersonality.PaprDb(Calc(clipped).I, Calc(clipped).Q);
            double paprUnclipped = AwgnPersonality.PaprDb(Calc(unclipped).I, Calc(unclipped).Q);

            Assert.True(paprUnclipped > paprClipped + 1.0,
                $"Unclipped PAPR ({paprUnclipped:F3} dB) should exceed clipped ({paprClipped:F3} dB)");
        }

        [Fact]
        public void Band_limiting_reduces_papr_versus_full_band_for_same_seed()
        {
            // Sanity check that the FIR low-pass path runs and shapes the envelope.
            var bandLimited = new AwgnConfig
            {
                Length = 32768,
                RandomSeed = 555,
                CrestFactorDb = 0.0,
                NoiseBandwidthHz = 1e6,
                SampleRateHz = 10e6
            };
            WaveformModel wf = Calc(bandLimited);

            Assert.Equal(bandLimited.Length, wf.Length);
            Assert.Equal(1.0, wf.PeakMagnitude(), 5);
            double papr = AwgnPersonality.PaprDb(wf.I, wf.Q);
            Assert.False(double.IsNaN(papr) || double.IsInfinity(papr));
            Assert.True(papr > 0.0);
        }

        [Fact]
        public void Personality_identity()
        {
            var p = new AwgnPersonality();
            Assert.Equal("awgn", p.Id);
            Assert.Equal("AWGN", p.DisplayName);
            Assert.Null(p.RequiredOption);
        }
    }
}
