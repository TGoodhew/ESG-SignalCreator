using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;
using EsgSignalCreator.Personalities.WimaxMobile;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    /// <summary>Tests for the v2 frame-structured mobile-WiMAX (802.16e) OFDMA frame (#193).</summary>
    public class WimaxMobileFramePersonalityTests
    {
        private const int FftN = 512;    // WimaxFftSize.Fft512
        private const int Occupied = 420;
        private const int Half = Occupied / 2;
        private const int Cp = FftN / 8; // CP ratio 1/8 => 64
        private const int SymLen = Cp + FftN;

        [Fact]
        public void Frame_length_and_sample_rate()
        {
            WaveformModel wf = Frame(sym: 10, preamble: true);
            Assert.Equal(FftN * 10.9375e3, wf.SampleRateHz, 1);
            Assert.Equal((10 + 1) * SymLen, wf.Length); // +1 preamble symbol
        }

        [Fact]
        public void No_preamble_drops_the_preamble_symbol()
        {
            WaveformModel wf = Frame(sym: 10, preamble: false);
            Assert.Equal(10 * SymLen, wf.Length);
        }

        [Fact]
        public void Preamble_excites_only_every_third_used_subcarrier()
        {
            WaveformModel wf = Frame(sym: 4, preamble: true);
            double[] re, im;
            Spectrum(wf, Cp, out re, out im); // preamble symbol data (after its CP)

            int i = 0;
            double refMag = 0;
            for (int d = -Half; d <= Half; d++)
            {
                if (d == 0) continue;
                double mag = Mag(re, im, d);
                if (i % 3 == 0)
                {
                    if (refMag == 0) refMag = mag;
                    Assert.True(mag > 1e-3, "every-3rd used subcarrier carries the preamble");
                }
                else
                {
                    Assert.True(mag < 1e-6, "other subcarriers are null in the preamble");
                }
                i++;
            }
            Assert.True(refMag > 1e-3);
        }

        [Fact]
        public void Data_symbols_carry_dl_pusc_pilots_at_cluster_positions_4_and_8()
        {
            WaveformModel wf = Frame(sym: 4, preamble: true);
            double[] re, im;
            Spectrum(wf, SymLen + Cp, out re, out im); // first data symbol

            Assert.True(Mag(re, im, 0) < 1e-6, "DC null");

            int i = 0;
            double pilotMag = 0;
            for (int d = -Half; d <= Half; d++)
            {
                if (d == 0) continue;
                int within = i % 14;
                if (within == 4 || within == 8)
                {
                    double mag = Mag(re, im, d);
                    int bin = d >= 0 ? d : FftN + d;
                    if (pilotMag == 0) pilotMag = mag;
                    Assert.True(mag > 1e-3, "pilot carries energy");
                    Assert.Equal(pilotMag, mag, 3);                 // all pilots equal magnitude
                    Assert.True(Math.Abs(im[bin]) < 0.05 * mag + 1e-6, "pilots are real BPSK");
                }
                i++;
            }
            Assert.True(pilotMag > 1e-3, "found DL-PUSC pilots");
        }

        [Fact]
        public void Frame_is_unit_peak()
        {
            WaveformModel wf = Frame(sym: 8, preamble: true);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Generic_mode_is_unchanged_when_not_frame_structured()
        {
            var cfg = new WimaxMobileConfig { FftSize = WimaxFftSize.Fft512, SymbolCount = 12, FrameStructured = false };
            var p = new WimaxMobilePersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);
            Assert.Equal(12 * SymLen, wf.Length);
        }

        // --- helpers ------------------------------------------------------------------

        private static WaveformModel Frame(int sym, bool preamble)
        {
            var cfg = new WimaxMobileConfig
            {
                FftSize = WimaxFftSize.Fft512,
                CyclicPrefixRatio = CpRatio.OneEighth,
                SymbolCount = sym,
                Modulation = Modulation.QAM16,
                FrameStructured = true,
                IncludePreamble = preamble
            };
            var p = new WimaxMobilePersonality();
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
