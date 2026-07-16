using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Lte;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured LTE TDD downlink (#189).</summary>
    public class LteTddFramePersonalityTests
    {
        private const int FftN = 512;   // 5 MHz, 25 RB
        private const int Cp0 = 40;
        private const int CpN = 36;
        private const int SymbolsPerSlot = 7;
        private const int SymbolsPerSubframe = 14;

        [Fact]
        public void Tdd_full_frame_length_is_10ms()
        {
            WaveformModel wf = Frame(cellId: 0, subframes: 10, ulDl: 1, ssf: 7);
            Assert.Equal(FftN * 15e3, wf.SampleRateHz, 3);
            Assert.Equal(FftN * 150, wf.Length);   // silent subframes still occupy their CP+FFT slots
        }

        [Fact]
        public void Uplink_subframes_are_silent()
        {
            // Config 1 = D S U U D D S U U D -> subframes 2,3,7,8 are uplink (silent).
            WaveformModel wf = Frame(cellId: 3, subframes: 10, ulDl: 1, ssf: 7);
            foreach (int sf in new[] { 2, 3, 7, 8 })
            {
                int mid = sf * SamplesPerSubframe() + SamplesPerSubframe() / 2;
                double mag = Math.Sqrt(wf.I[mid] * wf.I[mid] + wf.Q[mid] * wf.Q[mid]);
                Assert.Equal(0.0, mag, 6);
            }
            // A downlink subframe (0) is energised.
            int dl = 0 * SamplesPerSubframe() + SamplesPerSubframe() / 2;
            Assert.True(Math.Sqrt(wf.I[dl] * wf.I[dl] + wf.Q[dl] * wf.Q[dl]) > 1e-3);
        }

        [Fact]
        public void Special_subframe_guard_and_uppts_are_silent()
        {
            // Special-subframe config 7 (normal CP): DwPTS = 10 symbols -> symbols 10..13 are GP/UpPTS.
            WaveformModel wf = Frame(cellId: 0, subframes: 10, ulDl: 1, ssf: 7);
            int start = DataStart(1, 12);   // a GP/UpPTS symbol of the special subframe (subframe 1)
            double mag = Math.Sqrt(wf.I[start] * wf.I[start] + wf.Q[start] * wf.Q[start]);
            Assert.Equal(0.0, mag, 6);

            // But the DwPTS (symbol 5, < 10) is transmitted.
            int dwStart = DataStart(1, 5);
            Assert.True(Math.Sqrt(wf.I[dwStart] * wf.I[dwStart] + wf.Q[dwStart] * wf.Q[dwStart]) > 1e-3);
        }

        [Fact]
        public void Tdd_pss_is_in_symbol_2_of_the_special_subframe_and_matches_zadoff_chu()
        {
            int nid2 = 1; // cellId 1 => N_id_2 = 1 (u = 29)
            WaveformModel wf = Frame(cellId: 1, subframes: 10, ulDl: 1, ssf: 7);

            double[] re, im;
            SymbolSpectrum(wf, DataStart(1, 2), out re, out im); // subframe 1 (special), symbol 2 = PSS

            double[] zr = new double[62], zi = new double[62];
            ExpectedPss(nid2, zr, zi);

            double refRe = 0, refIm = 0; bool haveRef = false;
            for (int n = 0; n < 62; n++)
            {
                int d = n < 31 ? n - 31 : n - 30;
                int bin = d > 0 ? d : FftN + d;
                double rr = re[bin] * zr[n] + im[bin] * zi[n];
                double ri = im[bin] * zr[n] - re[bin] * zi[n];
                if (!haveRef) { refRe = rr; refIm = ri; haveRef = true; }
                else { Assert.Equal(refRe, rr, 3); Assert.Equal(refIm, ri, 3); }
            }
            Assert.True(Math.Sqrt(refRe * refRe + refIm * refIm) > 1e-3, "TDD PSS must be present");
        }

        [Fact]
        public void Tdd_sss_is_in_the_last_symbol_of_subframe_0_and_is_real()
        {
            WaveformModel wf = Frame(cellId: 5, subframes: 10, ulDl: 2, ssf: 5);
            double[] re, im;
            SymbolSpectrum(wf, DataStart(0, SymbolsPerSubframe - 1), out re, out im); // subframe 0, last symbol

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
        public void Different_ul_dl_configs_change_which_subframes_are_silent()
        {
            // Config 5 = D S U D D D D D D D: only subframe 2 is uplink. Subframe 3 (U in config 1) is DL here.
            WaveformModel wf = Frame(cellId: 0, subframes: 10, ulDl: 5, ssf: 7);
            int sf3 = 3 * SamplesPerSubframe() + SamplesPerSubframe() / 2;
            Assert.True(Math.Sqrt(wf.I[sf3] * wf.I[sf3] + wf.Q[sf3] * wf.Q[sf3]) > 1e-3, "subframe 3 is downlink in config 5");
            int sf2 = 2 * SamplesPerSubframe() + SamplesPerSubframe() / 2;
            Assert.Equal(0.0, Math.Sqrt(wf.I[sf2] * wf.I[sf2] + wf.Q[sf2] * wf.Q[sf2]), 6);
        }

        [Theory]
        [InlineData(7, 7)]   // ul/dl config out of range (0..6)
        [InlineData(1, 10)]  // special-subframe config out of range (0..9)
        public void Invalid_tdd_configs_are_rejected(int ulDl, int ssf)
        {
            var cfg = TddCfg(0, 10, ulDl, ssf, LteCyclicPrefix.Normal);
            var p = new LteTddPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Extended_cp_special_config_above_6_is_rejected()
        {
            var cfg = TddCfg(0, 10, 1, 7, LteCyclicPrefix.Extended); // ssf 7 invalid for extended CP
            var p = new LteTddPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Tdd_generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw5MHz, SymbolCount = 14, FrameStructured = false };
            var p = new LteTddPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(14 * (FftN + CpN), wf.Length);
        }

        // --- helpers ------------------------------------------------------------------

        private static LteConfig TddCfg(int cellId, int subframes, int ulDl, int ssf, LteCyclicPrefix cp) => new LteConfig
        {
            Bandwidth = LteBandwidth.Bw5MHz,
            Modulation = Modulation.QAM16,
            FrameStructured = true,
            PhysicalCellId = cellId,
            SubframeCount = subframes,
            CyclicPrefix = cp,
            TddUlDlConfig = ulDl,
            TddSpecialSubframeConfig = ssf
        };

        private static WaveformModel Frame(int cellId, int subframes, int ulDl, int ssf)
        {
            var p = new LteTddPersonality();
            p.LoadConfig(TddCfg(cellId, subframes, ulDl, ssf, LteCyclicPrefix.Normal));
            return p.Calculate(null);
        }

        private static int SamplesPerSubframe() => 2 * ((Cp0 + FftN) + 6 * (CpN + FftN));

        /// <summary>Sample offset of the FFT data (after the CP) of symbol <paramref name="symInSf"/> in subframe.</summary>
        private static int DataStart(int subframe, int symInSf)
        {
            int off = subframe * SamplesPerSubframe();
            for (int j = 0; j < symInSf; j++) off += ((j % SymbolsPerSlot == 0) ? Cp0 : CpN) + FftN;
            off += (symInSf % SymbolsPerSlot == 0) ? Cp0 : CpN;
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
