using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.GsmEdge;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class GsmEdgePersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_follow_symbol_count_and_rate()
        {
            var cfg = new GsmEdgeConfig { SamplesPerSymbol = 16, SymbolCount = 256 };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(256 * 16, wf.Length);
            Assert.Equal(cfg.SymbolRateHz * 16, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Gmsk_is_constant_envelope()
        {
            var cfg = new GsmEdgeConfig { SamplesPerSymbol = 16, SymbolCount = 300, Data = DataSource.PN9 };
            WaveformModel wf = Calc(cfg);

            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 3);
            }
        }

        [Fact]
        public void All_zeros_data_produces_a_constant_frequency_tone()
        {
            // A constant NRZ (-1) frequency pulse => constant phase slope => single tone.
            var cfg = new GsmEdgeConfig { SamplesPerSymbol = 16, SymbolCount = 64, Data = DataSource.AllZeros };
            WaveformModel wf = Calc(cfg);

            // Measure per-sample phase increment away from the filter transient; it should be ~constant.
            double prev = Math.Atan2(wf.Q[300], wf.I[300]);
            double sum = 0, sumSq = 0; int count = 0;
            for (int s = 301; s < 700; s++)
            {
                double ph = Math.Atan2(wf.Q[s], wf.I[s]);
                double d = ph - prev;
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d < -Math.PI) d += 2 * Math.PI;
                prev = ph;
                sum += d; sumSq += d * d; count++;
            }
            double mean = sum / count;
            double var = sumSq / count - mean * mean;
            Assert.True(var < 1e-6, $"phase increment should be near-constant (var={var:E3})");
        }

        [Fact]
        public void Pn9_data_is_deterministic()
        {
            var cfg = new GsmEdgeConfig { SamplesPerSymbol = 8, SymbolCount = 200, Data = DataSource.PN9 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++)
            {
                Assert.Equal(a.I[s], b.I[s], 6);
                Assert.Equal(a.Q[s], b.Q[s], 6);
            }
        }

        [Fact]
        public void Zero_symbol_count_is_rejected()
        {
            var cfg = new GsmEdgeConfig { SymbolCount = 0 };
            var p = new GsmEdgePersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        // ---- v2 (#186): EDGE 3π/8-rotated 8-PSK ----

        [Fact]
        public void Edge_output_length_and_sample_rate()
        {
            var cfg = new GsmEdgeConfig { Modulation = GsmModulation.Edge8Psk, SymbolCount = 200, SamplesPerSymbol = 16 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(200 * 16, wf.Length);
            Assert.Equal(cfg.SymbolRateHz * 16, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Edge_is_not_constant_envelope_unlike_gmsk()
        {
            var edge = Calc(new GsmEdgeConfig { Modulation = GsmModulation.Edge8Psk, SymbolCount = 400, SamplesPerSymbol = 16 });
            var gmsk = Calc(new GsmEdgeConfig { Modulation = GsmModulation.Gmsk, SymbolCount = 400, SamplesPerSymbol = 16 });

            // GMSK is constant-envelope (|s| = 1 everywhere); EDGE 8-PSK with pulse shaping dips well below peak.
            Assert.True(MinEnvelope(gmsk) > 0.95, "GMSK should be (near) constant envelope");
            Assert.True(MinEnvelope(edge) < 0.6, "EDGE 8-PSK should have envelope variation");
        }

        [Fact]
        public void Edge_differs_from_gmsk()
        {
            var edge = Calc(new GsmEdgeConfig { Modulation = GsmModulation.Edge8Psk, SymbolCount = 200, SamplesPerSymbol = 16 });
            var gmsk = Calc(new GsmEdgeConfig { Modulation = GsmModulation.Gmsk, SymbolCount = 200, SamplesPerSymbol = 16 });
            double maxDiff = 0;
            int n = Math.Min(edge.Length, gmsk.Length);
            for (int s = 0; s < n; s++) maxDiff = Math.Max(maxDiff, Math.Abs(edge.I[s] - gmsk.I[s]));
            Assert.True(maxDiff > 0.1, "EDGE 8-PSK must differ from GMSK");
        }

        private static double MinEnvelope(WaveformModel wf)
        {
            // Ignore the filter warm-up/tail edges where the pulse-shaped signal ramps.
            double min = double.MaxValue;
            int guard = 64;
            for (int s = guard; s < wf.Length - guard; s++)
                min = Math.Min(min, Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]));
            return min;
        }

        private static WaveformModel Calc(GsmEdgeConfig cfg)
        {
            var p = new GsmEdgePersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
