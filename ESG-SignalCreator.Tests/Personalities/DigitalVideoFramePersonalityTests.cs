using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.DigitalVideo;
using EsgSignalCreator.Personalities.WimaxFixed;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 DVB-T scattered-pilot signal (#196).</summary>
    public class DigitalVideoFramePersonalityTests
    {
        // DVB-T 2K mode: 2048-FFT, 1704 active carriers, guard 1/8 => CP 256.
        private const int FftN = 2048;
        private const int Occupied = 1704;
        private const int Half = Occupied / 2;
        private const int Cp = 256;
        private const int SymLen = Cp + FftN;

        [Fact]
        public void Frame_length_and_sample_rate()
        {
            WaveformModel wf = Frame(sym: 8);
            Assert.Equal(64e6 / 7.0, wf.SampleRateHz, 1);
            Assert.Equal(8 * SymLen, wf.Length);
        }

        [Fact]
        public void Scattered_pilots_are_real_and_shift_by_three_each_symbol()
        {
            WaveformModel wf = Frame(sym: 8);
            double[] re0, im0, re1, im1;
            Spectrum(wf, Cp, out re0, out im0);            // symbol 0: pilots where i%12 == 0
            Spectrum(wf, SymLen + Cp, out re1, out im1);   // symbol 1: pilots where i%12 == 3

            // Carrier i=12 (i%12==0): pilot in symbol 0 (real), data in symbol 1 (QPSK => complex).
            Assert.True(IsReal(re0, im0, 12), "i=12 is a pilot in symbol 0");
            Assert.False(IsReal(re1, im1, 12), "i=12 is data in symbol 1");

            // Carrier i=15 (i%12==3): data in symbol 0, pilot in symbol 1.
            Assert.False(IsReal(re0, im0, 15), "i=15 is data in symbol 0");
            Assert.True(IsReal(re1, im1, 15), "i=15 is a pilot in symbol 1");
        }

        [Fact]
        public void All_scattered_pilot_positions_of_a_symbol_are_real()
        {
            WaveformModel wf = Frame(sym: 8);
            double[] re, im;
            Spectrum(wf, 2 * SymLen + Cp, out re, out im); // symbol 2: pilots where i%12 == 6

            int pilots = 0;
            for (int i = 0; i < Occupied; i++)
            {
                if (i % 12 != 6) continue;
                Assert.True(IsReal(re, im, i), "every i%12==6 carrier is a pilot in symbol 2");
                pilots++;
            }
            Assert.True(pilots > 100, "found the scattered pilots"); // ~1704/12 = 142
        }

        [Fact]
        public void Dc_and_guard_bands_are_null()
        {
            WaveformModel wf = Frame(sym: 8);
            double[] re, im;
            Spectrum(wf, Cp, out re, out im);
            Assert.True(Math.Sqrt(re[0] * re[0] + im[0] * im[0]) < 1e-6, "DC null");
            Assert.True(Math.Sqrt(re[1000] * re[1000] + im[1000] * im[1000]) < 1e-6, "guard null (bin beyond used band)");
        }

        [Fact]
        public void Frame_is_unit_peak()
        {
            WaveformModel wf = Frame(sym: 6);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new DigitalVideoConfig
            {
                Mode = DvbtMode.Mode2K, GuardInterval = CpRatio.OneEighth,
                SymbolCount = 8, Modulation = Modulation.QPSK, FrameStructured = false
            };
            var p = new DigitalVideoPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(8 * SymLen, wf.Length);
        }

        // --- helpers ------------------------------------------------------------------

        private static WaveformModel Frame(int sym)
        {
            var cfg = new DigitalVideoConfig
            {
                Mode = DvbtMode.Mode2K,
                GuardInterval = CpRatio.OneEighth,
                SymbolCount = sym,
                Modulation = Modulation.QPSK, // QPSK data is always complex, so pilots stand out as real
                FrameStructured = true
            };
            var p = new DigitalVideoPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }

        private static bool IsReal(double[] re, double[] im, int carrierIndex)
        {
            int d = (carrierIndex < Half) ? carrierIndex - Half : carrierIndex - Half + 1;
            int bin = d >= 0 ? d : FftN + d;
            double mag = Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]);
            return mag > 1e-3 && Math.Abs(im[bin]) < 0.05 * mag;
        }

        private static void Spectrum(WaveformModel wf, int start, out double[] re, out double[] im)
        {
            re = new double[FftN]; im = new double[FftN];
            for (int k = 0; k < FftN; k++) { re[k] = wf.I[start + k]; im[k] = wf.Q[start + k]; }
            Fft.Forward(re, im);
        }
    }
}
