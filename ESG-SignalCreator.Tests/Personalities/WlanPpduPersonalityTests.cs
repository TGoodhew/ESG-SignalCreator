using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Wlan;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured 802.11a/g PPDU (#191).</summary>
    public class WlanPpduPersonalityTests
    {
        private const int FftN = 64;
        private const int Gi2 = 32;
        private const int PreambleLen = Gi2 + 2 * FftN; // 160

        [Fact]
        public void Length_and_sample_rate_with_long_gi_and_preamble()
        {
            WaveformModel wf = Ppdu(sym: 10, gi: WlanGuardInterval.Long, preamble: true);
            Assert.Equal(20e6, wf.SampleRateHz, 3);
            Assert.Equal(PreambleLen + 10 * (16 + FftN), wf.Length); // long GI = 16
        }

        [Fact]
        public void Short_gi_shortens_the_data_symbols()
        {
            WaveformModel wf = Ppdu(sym: 10, gi: WlanGuardInterval.Short, preamble: true);
            Assert.Equal(PreambleLen + 10 * (8 + FftN), wf.Length); // short GI = 8
        }

        [Fact]
        public void No_preamble_drops_the_ltf_field()
        {
            WaveformModel wf = Ppdu(sym: 10, gi: WlanGuardInterval.Long, preamble: false);
            Assert.Equal(10 * (16 + FftN), wf.Length);
        }

        [Fact]
        public void Ltf_two_long_symbols_are_identical()
        {
            WaveformModel wf = Ppdu(sym: 4, gi: WlanGuardInterval.Long, preamble: true);
            // The L-LTF field is [GI2][LTF][LTF]; the two 64-sample copies must match sample-for-sample.
            for (int k = 0; k < FftN; k++)
            {
                Assert.Equal(wf.I[Gi2 + k], wf.I[Gi2 + FftN + k], 6);
                Assert.Equal(wf.Q[Gi2 + k], wf.Q[Gi2 + FftN + k], 6);
            }
        }

        [Fact]
        public void Ltf_spectrum_is_real_and_matches_the_training_sequence_signs()
        {
            int[] ltf =
            {
                1, 1, -1, -1, 1, 1, -1, 1, -1, 1, 1, 1, 1, 1, 1, -1, -1, 1, 1, -1, 1, -1, 1, 1, 1, 1,
                0,
                1, -1, -1, 1, 1, -1, 1, -1, 1, -1, -1, -1, -1, -1, 1, 1, -1, -1, 1, -1, 1, -1, 1, 1, 1, 1
            };
            WaveformModel wf = Ppdu(sym: 4, gi: WlanGuardInterval.Long, preamble: true);

            double[] re, im;
            Spectrum(wf, Gi2, out re, out im); // first LTF 64-sample symbol

            for (int k = -26; k <= 26; k++)
            {
                if (k == 0) continue;
                int bin = k >= 0 ? k : FftN + k;
                double mag = Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]);
                Assert.True(mag > 1e-4, "used subcarrier must carry energy");
                Assert.True(Math.Abs(im[bin]) < 0.1 * mag + 1e-6, "L-LTF is real");
                Assert.Equal(ltf[k + 26], Math.Sign(re[bin]));
            }
        }

        [Fact]
        public void Data_symbols_carry_pilots_at_plus_minus_7_and_21_with_dc_null()
        {
            WaveformModel wf = Ppdu(sym: 4, gi: WlanGuardInterval.Long, preamble: true);
            int dataStart = PreambleLen + 16; // first data symbol, after its long-GI cyclic prefix

            double[] re, im;
            Spectrum(wf, dataStart, out re, out im);

            // DC nulled.
            Assert.True(Math.Sqrt(re[0] * re[0] + im[0] * im[0]) < 1e-6);

            // Symbol 0 polarity is +1, so pilots are {+,+,+,-} at {-21,-7,7,21}, equal magnitude, real.
            double Bin(int k, out double imag) { int b = k >= 0 ? k : FftN + k; imag = im[b]; return re[b]; }
            double m21 = Bin(-21, out double i1), m7 = Bin(-7, out double i2), p7 = Bin(7, out double i3), p21 = Bin(21, out double i4);

            double a = Math.Abs(m21);
            Assert.True(a > 1e-3, "pilots carry energy");
            Assert.Equal(a, Math.Abs(m7), 3);
            Assert.Equal(a, Math.Abs(p7), 3);
            Assert.Equal(a, Math.Abs(p21), 3);
            Assert.True(m21 > 0 && m7 > 0 && p7 > 0 && p21 < 0, "pilot base polarity {+,+,+,-}");
            foreach (double imag in new[] { i1, i2, i3, i4 }) Assert.True(Math.Abs(imag) < 0.05 * a + 1e-6, "pilots are real");
        }

        [Fact]
        public void Frame_is_unit_peak()
        {
            WaveformModel wf = Ppdu(sym: 8, gi: WlanGuardInterval.Long, preamble: true);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Frame_structured_rejects_40mhz()
        {
            var cfg = new WlanConfig { Bandwidth = WlanBandwidth.Bw40MHz, FrameStructured = true };
            var p = new WlanPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new WlanConfig { Bandwidth = WlanBandwidth.Bw20MHz, SymbolCount = 16, FrameStructured = false };
            var p = new WlanPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(16 * (FftN + 16), wf.Length); // generic engine, 20 MHz CP = 16
        }

        // --- helpers ------------------------------------------------------------------

        private static WaveformModel Ppdu(int sym, WlanGuardInterval gi, bool preamble)
        {
            var cfg = new WlanConfig
            {
                Bandwidth = WlanBandwidth.Bw20MHz,
                SymbolCount = sym,
                Modulation = Modulation.QAM16,
                FrameStructured = true,
                GuardInterval = gi,
                IncludeLtfPreamble = preamble
            };
            var p = new WlanPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private static void Spectrum(WaveformModel wf, int start, out double[] re, out double[] im)
        {
            re = new double[FftN]; im = new double[FftN];
            for (int k = 0; k < FftN; k++) { re[k] = wf.I[start + k]; im[k] = wf.Q[start + k]; }
            Fft.Forward(re, im);
        }
    }
}
