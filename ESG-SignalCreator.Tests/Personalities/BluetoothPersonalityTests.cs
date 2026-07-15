using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Bluetooth;
using EsgSignalCreator.Personalities.CustomMod;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class BluetoothPersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_follow_config()
        {
            var cfg = new BluetoothConfig { SamplesPerSymbol = 16, SymbolCount = 256, SymbolRateHz = 1e6 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(256 * 16, wf.Length);
            Assert.Equal(16e6, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Gfsk_is_constant_envelope()
        {
            var cfg = new BluetoothConfig { SamplesPerSymbol = 16, SymbolCount = 300, Data = DataSource.PN9 };
            WaveformModel wf = Calc(cfg);
            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 3);
            }
        }

        [Fact]
        public void Higher_modulation_index_gives_larger_phase_deviation()
        {
            // All-ones data => steady frequency; a larger index means a larger per-sample phase step.
            double slopeLow = SteadySlope(0.20);
            double slopeHigh = SteadySlope(0.50);
            Assert.True(Math.Abs(slopeHigh) > Math.Abs(slopeLow),
                $"index 0.5 slope ({slopeHigh:F5}) should exceed index 0.2 slope ({slopeLow:F5})");
        }

        [Fact]
        public void Pn9_is_deterministic()
        {
            var cfg = new BluetoothConfig { SamplesPerSymbol = 8, SymbolCount = 200, Data = DataSource.PN9 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        [Fact]
        public void Nonpositive_index_is_rejected()
        {
            var cfg = new BluetoothConfig { ModulationIndex = 0 };
            var p = new BluetoothPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static double SteadySlope(double index)
        {
            var cfg = new BluetoothConfig
            {
                SamplesPerSymbol = 16,
                SymbolCount = 64,
                ModulationIndex = index,
                Data = DataSource.AllOnes
            };
            WaveformModel wf = Calc(cfg);
            double p0 = Math.Atan2(wf.Q[400], wf.I[400]);
            double p1 = Math.Atan2(wf.Q[401], wf.I[401]);
            double d = p1 - p0;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            return d;
        }

        private static WaveformModel Calc(BluetoothConfig cfg)
        {
            var p = new BluetoothPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
