using System;
using System.Collections.Generic;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class CustomModPersonalityTests
    {
        // ---------- Symbol mapper ----------

        [Fact]
        public void Qpsk_mapper_yields_four_distinct_unit_circle_points()
        {
            var mapper = new SymbolMapper(Modulation.QPSK);
            Assert.Equal(2, mapper.BitsPerSymbol);

            var points = new HashSet<(long, long)>();
            for (int b0 = 0; b0 < 2; b0++)
            for (int b1 = 0; b1 < 2; b1++)
            {
                mapper.Map(new[] { b0, b1 }, out double i, out double q);
                double mag = Math.Sqrt(i * i + q * q);
                Assert.Equal(1.0, mag, 6); // on the unit circle
                points.Add((Round(i), Round(q)));
            }

            Assert.Equal(4, points.Count); // exactly four distinct points
        }

        [Fact]
        public void Bpsk_mapper_yields_two_distinct_points_on_the_I_axis()
        {
            var mapper = new SymbolMapper(Modulation.BPSK);
            Assert.Equal(1, mapper.BitsPerSymbol);

            mapper.Map(new[] { 0 }, out double i0, out double q0);
            mapper.Map(new[] { 1 }, out double i1, out double q1);

            Assert.Equal(0.0, q0, 12);
            Assert.Equal(0.0, q1, 12);
            Assert.Equal(1.0, Math.Abs(i0), 12);
            Assert.Equal(1.0, Math.Abs(i1), 12);
            Assert.NotEqual(Round(i0), Round(i1)); // two distinct points
        }

        [Fact]
        public void Qam16_average_symbol_power_is_unity()
        {
            var mapper = new SymbolMapper(Modulation.QAM16);
            Assert.Equal(4, mapper.BitsPerSymbol);

            double sumPower = 0.0;
            int count = 0;
            for (int v = 0; v < 16; v++)
            {
                int[] bits = BitsMsbFirst(v, 4);
                mapper.Map(bits, out double i, out double q);
                sumPower += i * i + q * q;
                count++;
            }

            Assert.Equal(16, count);
            Assert.Equal(1.0, sumPower / count, 9); // E{|s|^2} == 1
        }

        [Fact]
        public void Qam64_average_symbol_power_is_unity()
        {
            var mapper = new SymbolMapper(Modulation.QAM64);
            double sumPower = 0.0;
            for (int v = 0; v < 64; v++)
            {
                mapper.Map(BitsMsbFirst(v, 6), out double i, out double q);
                sumPower += i * i + q * q;
            }
            Assert.Equal(1.0, sumPower / 64, 9);
        }

        // ---------- PRBS / data sources ----------

        [Fact]
        public void Pn9_is_deterministic_and_not_all_same()
        {
            var a = Prbs.CreateBitGenerator(DataSource.PN9);
            var b = Prbs.CreateBitGenerator(DataSource.PN9);

            int ones = 0;
            for (int k = 0; k < 511; k++)
            {
                int ba = a();
                int bb = b();
                Assert.Equal(ba, bb); // deterministic
                ones += ba;
            }
            Assert.True(ones > 0 && ones < 511, "PN9 must contain both 0s and 1s");
        }

        [Fact]
        public void Pn9_repeats_with_period_511()
        {
            var gen = Prbs.CreateBitGenerator(DataSource.PN9);
            int period = Prbs.Period(DataSource.PN9);
            Assert.Equal(511, period);

            var first = new int[period];
            for (int k = 0; k < period; k++) first[k] = gen();

            // Next full period must reproduce the first.
            for (int k = 0; k < period; k++)
                Assert.Equal(first[k], gen());
        }

        [Fact]
        public void AllZeros_and_AllOnes_sources_are_constant()
        {
            var z = Prbs.CreateBitGenerator(DataSource.AllZeros);
            var o = Prbs.CreateBitGenerator(DataSource.AllOnes);
            for (int k = 0; k < 100; k++)
            {
                Assert.Equal(0, z());
                Assert.Equal(1, o());
            }
        }

        // ---------- Personality pipeline ----------

        [Fact]
        public void Output_sample_count_equals_symbolcount_times_samples_per_symbol()
        {
            var cfg = new CustomModConfig
            {
                Modulation = Modulation.QPSK,
                SymbolRateHz = 1e6,
                SamplesPerSymbol = 8,
                Shape = PulseShape.RootRaisedCosine,
                Alpha = 0.35,
                FilterSpanSymbols = 8,
                SymbolCount = 512,
                Data = DataSource.PN9
            };

            WaveformModel wf = Calc(cfg);

            // 'same'-length convolution => length is exactly SymbolCount * SamplesPerSymbol
            // (filter delay removed, tail not appended) — see CustomModPersonality docs.
            Assert.Equal(512 * 8, wf.Length);
            Assert.Equal(1e6 * 8, wf.SampleRateHz);
        }

        [Fact]
        public void Rectangular_shaping_produces_expected_length_and_held_symbols()
        {
            var cfg = new CustomModConfig
            {
                Modulation = Modulation.BPSK,
                SamplesPerSymbol = 4,
                Shape = PulseShape.Rectangular,
                SymbolCount = 16,
                Data = DataSource.AllZeros
            };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(16 * 4, wf.Length);

            // AllZeros + BPSK => every symbol is +1 on I; after peak-normalize it stays +1.
            for (int n = 0; n < wf.Length; n++)
            {
                Assert.Equal(1.0, wf.I[n], 5);
                Assert.Equal(0.0, wf.Q[n], 5);
            }
        }

        [Fact]
        public void Peak_magnitude_is_normalized_to_one()
        {
            var cfg = new CustomModConfig
            {
                Modulation = Modulation.QAM16,
                SamplesPerSymbol = 8,
                Shape = PulseShape.RootRaisedCosine,
                Alpha = 0.25,
                SymbolCount = 256,
                Data = DataSource.PN9
            };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Msk_is_constant_envelope()
        {
            var cfg = new CustomModConfig
            {
                Modulation = Modulation.MSK,
                SamplesPerSymbol = 8,
                SymbolCount = 200,
                Data = DataSource.PN9
            };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(200 * 8, wf.Length);

            // Constant-envelope: every sample magnitude ~ 1 after peak normalization.
            for (int n = 0; n < wf.Length; n++)
            {
                double mag = Math.Sqrt((double)wf.I[n] * wf.I[n] + (double)wf.Q[n] * wf.Q[n]);
                Assert.Equal(1.0, mag, 3);
            }
        }

        [Fact]
        public void Progress_is_reported_and_reaches_100()
        {
            var cfg = new CustomModConfig { SymbolCount = 64, SamplesPerSymbol = 4 };
            // Progress<T> posts asynchronously, so use a synchronous collector for deterministic checks.
            var collected = new List<int>();
            var sync = new SyncProgress(collected.Add);

            var p2 = new CustomModPersonality();
            p2.LoadConfig(cfg);
            p2.Calculate(sync);

            Assert.NotEmpty(collected);
            Assert.Equal(100, collected[collected.Count - 1]);
        }

        // ---------- helpers ----------

        private static WaveformModel Calc(CustomModConfig cfg)
        {
            var p = new CustomModPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private static long Round(double x) => (long)Math.Round(x * 1e6);

        private static int[] BitsMsbFirst(int value, int width)
        {
            var bits = new int[width];
            for (int b = 0; b < width; b++)
                bits[b] = (value >> (width - 1 - b)) & 1;
            return bits;
        }

        private sealed class SyncProgress : IProgress<int>
        {
            private readonly Action<int> _onReport;
            public SyncProgress(Action<int> onReport) { _onReport = onReport; }
            public void Report(int value) => _onReport(value);
        }
    }
}
