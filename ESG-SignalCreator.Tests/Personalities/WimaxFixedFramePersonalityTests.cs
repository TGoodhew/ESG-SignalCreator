using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured fixed-WiMAX (802.16-2004) 256-FFT frame (#192).</summary>
    public class WimaxFixedFramePersonalityTests
    {
        private const int FftN = 256;
        private static readonly int[] Pilots = { -88, -63, -38, -13, 13, 38, 63, 88 };

        [Fact]
        public void Frame_length_and_sample_rate()
        {
            var wf = Frame(bwHz: 3.5e6, cp: CpRatio.OneEighth, sym: 10, preamble: true);
            int cpLen = FftN / 8;                       // CP ratio 1/8
            Assert.Equal(3.5e6 * 8.0 / 7.0, wf.SampleRateHz, 1);
            Assert.Equal((10 + 1) * (cpLen + FftN), wf.Length); // +1 preamble symbol
        }

        [Fact]
        public void No_preamble_drops_the_preamble_symbol()
        {
            var wf = Frame(bwHz: 3.5e6, cp: CpRatio.OneEighth, sym: 10, preamble: false);
            int cpLen = FftN / 8;
            Assert.Equal(10 * (cpLen + FftN), wf.Length);
        }

        [Fact]
        public void Data_symbol_has_pilots_at_the_standard_positions_and_nulled_dc_and_guards()
        {
            var wf = Frame(bwHz: 3.5e6, cp: CpRatio.OneEighth, sym: 4, preamble: true);
            int cpLen = FftN / 8;
            int dataStart = (1 /*preamble*/) * (cpLen + FftN) + cpLen; // first data symbol, after its CP

            double[] re, im;
            Spectrum(wf, dataStart, out re, out im);

            // DC and the guard bands (|k| > 100) are nulled.
            Assert.True(Mag(re, im, 0) < 1e-6, "DC null");
            Assert.True(Mag(re, im, 110) < 1e-6, "upper guard null");
            Assert.True(Mag(re, im, -110) < 1e-6, "lower guard null");

            // The 8 pilots are BPSK (real) with equal magnitude.
            double a = Mag(re, im, Pilots[0]);
            Assert.True(a > 1e-3, "pilots carry energy");
            foreach (int k in Pilots)
            {
                int bin = k >= 0 ? k : FftN + k;
                Assert.Equal(a, Mag(re, im, k), 3);
                Assert.True(Math.Abs(im[bin]) < 0.05 * a + 1e-6, "pilots are real BPSK");
            }
        }

        [Fact]
        public void Preamble_has_four_identical_quarter_symbol_repetitions()
        {
            // Every-4th-subcarrier excitation => the 256-sample OFDM symbol is 4 identical 64-sample blocks.
            var wf = Frame(bwHz: 3.5e6, cp: CpRatio.OneEighth, sym: 4, preamble: true);
            int cpLen = FftN / 8;
            int start = cpLen; // preamble symbol data (after its CP)

            for (int k = 0; k < 64; k++)
            {
                Assert.Equal(wf.I[start + k], wf.I[start + 64 + k], 5);
                Assert.Equal(wf.I[start + k], wf.I[start + 128 + k], 5);
                Assert.Equal(wf.I[start + k], wf.I[start + 192 + k], 5);
            }
        }

        [Fact]
        public void Frame_is_unit_peak()
        {
            var wf = Frame(bwHz: 3.5e6, cp: CpRatio.OneEighth, sym: 8, preamble: true);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new WimaxFixedConfig { ChannelBandwidthHz = 3.5e6, SymbolCount = 16, FrameStructured = false };
            var p = new WimaxFixedPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            int cpLen = FftN / 8;
            Assert.Equal(16 * (cpLen + FftN), wf.Length);
        }

        // --- helpers ------------------------------------------------------------------

        private static WaveformModel Frame(double bwHz, CpRatio cp, int sym, bool preamble)
        {
            var cfg = new WimaxFixedConfig
            {
                ChannelBandwidthHz = bwHz,
                CyclicPrefixRatio = cp,
                SymbolCount = sym,
                Modulation = Modulation.QAM16,
                FrameStructured = true,
                IncludePreamble = preamble
            };
            var p = new WimaxFixedPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private static double Mag(double[] re, double[] im, int k)
        {
            int bin = k >= 0 ? k : FftN + k;
            return Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]);
        }

        private static void Spectrum(WaveformModel wf, int start, out double[] re, out double[] im)
        {
            re = new double[FftN]; im = new double[FftN];
            for (int k = 0; k < FftN; k++) { re[k] = wf.I[start + k]; im[k] = wf.Q[start + k]; }
            Fft.Forward(re, im);
        }
    }
}
