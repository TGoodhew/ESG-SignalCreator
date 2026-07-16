using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Tdmb;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured T-DMB / DAB transmission frame (#195).</summary>
    public class TdmbFramePersonalityTests
    {
        // DAB Mode III: 256-FFT, 192 active carriers, guard 63.
        private const int FftN = 256;
        private const int Half = 96;
        private const int Cp = 63;
        private const int SymLen = Cp + FftN; // 319

        [Fact]
        public void Frame_length_and_sample_rate()
        {
            WaveformModel wf = Frame(sym: 8);
            Assert.Equal(2.048e6, wf.SampleRateHz, 1);
            Assert.Equal((2 + 8) * SymLen, wf.Length); // null + phase-reference + 8 data symbols
        }

        [Fact]
        public void Null_symbol_is_silent()
        {
            WaveformModel wf = Frame(sym: 8);
            for (int k = 0; k < SymLen; k++)
                Assert.True(Math.Sqrt(wf.I[k] * wf.I[k] + wf.Q[k] * wf.Q[k]) < 1e-6, "null symbol must be silent");
            // The phase-reference symbol that follows carries energy.
            int prs = SymLen + Cp + 50;
            Assert.True(Math.Sqrt(wf.I[prs] * wf.I[prs] + wf.Q[prs] * wf.Q[prs]) > 1e-4);
        }

        [Fact]
        public void Data_symbols_are_constant_modulus_dqpsk_with_dc_and_guard_null()
        {
            WaveformModel wf = Frame(sym: 8);
            double[] re, im;
            Spectrum(wf, 2 * SymLen + Cp, out re, out im); // first data symbol

            Assert.True(Mag(re, im, 0) < 1e-6, "DC null");
            Assert.True(Mag(re, im, 120) < 1e-6, "guard null (|k| > 96)");

            double a = Mag(re, im, 1);
            Assert.True(a > 1e-3, "active carriers carry energy");
            for (int d = -Half; d <= Half; d++)
            {
                if (d == 0) continue;
                Assert.Equal(a, Mag(re, im, d), 3); // DQPSK => unit modulus on every active carrier
            }
        }

        [Fact]
        public void Consecutive_symbol_phase_difference_is_a_multiple_of_90_degrees()
        {
            // DQPSK: phase(k, l) - phase(k, l-1) is a QPSK delta (0, ±90, 180 degrees).
            WaveformModel wf = Frame(sym: 8);
            double[] re0, im0, re1, im1;
            Spectrum(wf, SymLen + Cp, out re0, out im0);       // phase-reference symbol
            Spectrum(wf, 2 * SymLen + Cp, out re1, out im1);   // first data symbol

            for (int d = -Half; d <= Half; d++)
            {
                if (d == 0) continue;
                int bin = d >= 0 ? d : FftN + d;
                double p0 = Math.Atan2(im0[bin], re0[bin]);
                double p1 = Math.Atan2(im1[bin], re1[bin]);
                double diff = p1 - p0;
                // Reduce to the nearest multiple of pi/2 and check the residual is ~0.
                double steps = diff / (Math.PI / 2.0);
                double residual = steps - Math.Round(steps);
                Assert.True(Math.Abs(residual) < 0.02, "phase step must be a multiple of 90 degrees");
            }
        }

        [Fact]
        public void Frame_is_unit_peak()
        {
            WaveformModel wf = Frame(sym: 8);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new TdmbConfig { Mode = DabMode.ModeIII, SymbolCount = 16, FrameStructured = false };
            var p = new TdmbPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(16 * SymLen, wf.Length);
        }

        // --- helpers ------------------------------------------------------------------

        private static WaveformModel Frame(int sym)
        {
            var cfg = new TdmbConfig { Mode = DabMode.ModeIII, SymbolCount = sym, FrameStructured = true };
            var p = new TdmbPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private static double Mag(double[] re, double[] im, int d)
        {
            int bin = d >= 0 ? d : FftN + d;
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
