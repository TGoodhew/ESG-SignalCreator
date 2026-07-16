using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Lte;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured LTE FDD downlink (#188).</summary>
    public class LteFramePersonalityTests
    {
        // FFT size and normal-CP lengths for the 5 MHz bandwidth (25 RB), used to locate symbols.
        private const int FftN = 512;
        private const int Cp0 = 40;   // round(160 * 512/2048)
        private const int CpN = 36;   // round(144 * 512/2048)

        [Fact]
        public void Full_frame_length_is_10ms_and_sample_rate_matches()
        {
            WaveformModel wf = Frame(cellId: 0, subframes: 10, LteCyclicPrefix.Normal);
            double sr = FftN * 15e3;
            Assert.Equal(sr, wf.SampleRateHz, 3);
            Assert.Equal((int)(sr * 0.010), wf.Length);       // 10 ms radio frame
            Assert.Equal(FftN * 150, wf.Length);               // equivalently FFT × 150
        }

        [Fact]
        public void Extended_cp_frame_is_also_10ms()
        {
            WaveformModel wf = Frame(cellId: 1, subframes: 10, LteCyclicPrefix.Extended);
            Assert.Equal(FftN * 150, wf.Length);               // CP overhead is the same over 10 ms
        }

        [Fact]
        public void Frame_is_unit_peak_and_finite()
        {
            WaveformModel wf = Frame(cellId: 0, subframes: 1, LteCyclicPrefix.Normal);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
            for (int s = 0; s < wf.Length; s++)
                Assert.False(float.IsNaN(wf.I[s]) || float.IsNaN(wf.Q[s]));
        }

        [Fact]
        public void Pss_lands_on_the_central_62_subcarriers_matching_the_zadoff_chu_sequence()
        {
            int nid2 = 2; // cellId 2 => N_id_2 = 2 (u = 34)
            WaveformModel wf = Frame(cellId: 2, subframes: 1, LteCyclicPrefix.Normal);

            double[] re, im;
            SymbolSpectrum(wf, SymbolIndex(6), out re, out im); // PSS is the last symbol of slot 0

            double[] zr = new double[62], zi = new double[62];
            ExpectedPss(nid2, zr, zi);

            // The measured central-62 subcarriers must equal the ZC sequence up to a single global
            // complex scale (the frame's peak-normalization). Check the ratio is constant.
            double refRe = 0, refIm = 0; bool haveRef = false;
            for (int n = 0; n < 62; n++)
            {
                int d = n < 31 ? n - 31 : n - 30;
                int bin = d > 0 ? d : FftN + d;
                // ratio = measured / expected  (expected has unit modulus, so divide by conj)
                double mr = re[bin], mi = im[bin];
                double rr = mr * zr[n] + mi * zi[n];   // measured * conj(expected)
                double ri = mi * zr[n] - mr * zi[n];
                if (!haveRef) { refRe = rr; refIm = ri; haveRef = true; }
                else { Assert.Equal(refRe, rr, 3); Assert.Equal(refIm, ri, 3); }
            }
            Assert.True(Math.Sqrt(refRe * refRe + refIm * refIm) > 1e-3, "PSS energy must be present");
        }

        [Fact]
        public void Sss_symbol_central_subcarriers_are_real_bpsk()
        {
            WaveformModel wf = Frame(cellId: 5, subframes: 1, LteCyclicPrefix.Normal);

            double[] re, im;
            SymbolSpectrum(wf, SymbolIndex(5), out re, out im); // SSS is the symbol before PSS

            // SSS is BPSK on the I axis, so the central-62 bins are (near) purely real.
            for (int n = 0; n < 62; n++)
            {
                int d = n < 31 ? n - 31 : n - 30;
                int bin = d > 0 ? d : FftN + d;
                double mag = Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]);
                Assert.True(mag > 1e-4, "SSS subcarrier must carry energy");
                Assert.True(Math.Abs(im[bin]) < 0.15 * mag + 1e-6, "SSS must be (near) real");
            }
        }

        [Fact]
        public void Cell_id_changes_the_waveform()
        {
            WaveformModel a = Frame(cellId: 0, subframes: 1, LteCyclicPrefix.Normal);
            WaveformModel b = Frame(cellId: 7, subframes: 1, LteCyclicPrefix.Normal);
            double maxDiff = 0;
            for (int s = 0; s < a.Length; s++) maxDiff = Math.Max(maxDiff, Math.Abs(a.I[s] - b.I[s]));
            Assert.True(maxDiff > 0.05, "PSS/SSS/CRS depend on the cell ID");
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw5MHz, SymbolCount = 14, FrameStructured = false };
            var p = new LteFddPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(14 * (FftN + CpN), wf.Length); // generic engine uses a single CP length
        }

        [Theory]
        [InlineData(504)]
        [InlineData(-1)]
        public void Invalid_cell_id_is_rejected(int cellId)
        {
            var cfg = FrameCfg(cellId, 1, LteCyclicPrefix.Normal);
            var p = new LteFddPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Zero_subframes_is_rejected()
        {
            var cfg = FrameCfg(0, 0, LteCyclicPrefix.Normal);
            var p = new LteFddPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        // --- helpers ------------------------------------------------------------------

        private static LteConfig FrameCfg(int cellId, int subframes, LteCyclicPrefix cp) => new LteConfig
        {
            Bandwidth = LteBandwidth.Bw5MHz,
            Modulation = Modulation.QAM16,
            FrameStructured = true,
            PhysicalCellId = cellId,
            SubframeCount = subframes,
            CyclicPrefix = cp
        };

        private static WaveformModel Frame(int cellId, int subframes, LteCyclicPrefix cp)
        {
            var p = new LteFddPersonality();
            p.LoadConfig(FrameCfg(cellId, subframes, cp));
            return p.Calculate(null);
        }

        /// <summary>Sample offset of the FFT data (after the CP) of symbol <paramref name="l"/> in slot 0.</summary>
        private static int SymbolIndex(int l)
        {
            int off = 0;
            for (int j = 0; j < l; j++) off += (j == 0 ? Cp0 : CpN) + FftN;
            off += (l == 0 ? Cp0 : CpN); // skip this symbol's CP to reach its data
            return off;
        }

        private static void SymbolSpectrum(WaveformModel wf, int dataStart, out double[] re, out double[] im)
        {
            re = new double[FftN]; im = new double[FftN];
            for (int k = 0; k < FftN; k++) { re[k] = wf.I[dataStart + k]; im[k] = wf.Q[dataStart + k]; }
            Fft.Forward(re, im);
        }

        private static void ExpectedPss(int nid2, double[] outRe, double[] outIm)
        {
            int u = nid2 == 0 ? 25 : (nid2 == 1 ? 29 : 34);
            for (int n = 0; n < 62; n++)
            {
                double arg = n <= 30
                    ? -Math.PI * u * n * (n + 1) / 63.0
                    : -Math.PI * u * (n + 1) * (n + 2) / 63.0;
                outRe[n] = Math.Cos(arg);
                outIm[n] = Math.Sin(arg);
            }
        }
    }
}
