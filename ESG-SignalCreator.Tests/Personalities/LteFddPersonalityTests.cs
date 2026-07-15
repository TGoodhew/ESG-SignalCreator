using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Lte;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class LteFddPersonalityTests
    {
        [Fact]
        public void Bandwidth_selects_fft_and_sample_rate()
        {
            // 10 MHz => FFT 1024, 15 kHz spacing => 15.36 MHz sample rate, symbol = 1024 + 72 CP.
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw10MHz, SymbolCount = 7 };
            WaveformModel wf = Calc(cfg);

            Assert.Equal(15.36e6, wf.SampleRateHz, 0);
            Assert.Equal(7 * (1024 + 72), wf.Length);
        }

        [Fact]
        public void Twenty_mhz_uses_2048_fft_and_30p72_mhz()
        {
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw20MHz, SymbolCount = 2 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(30.72e6, wf.SampleRateHz, 0);
            Assert.Equal(2 * (2048 + 144), wf.Length);
        }

        [Fact]
        public void Peak_magnitude_is_normalized()
        {
            WaveformModel wf = Calc(new LteConfig { Bandwidth = LteBandwidth.Bw5MHz, SymbolCount = 14 });
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Cyclic_prefix_matches_symbol_tail()
        {
            // The CP is a copy of the last CP samples of the IFFT block; verify the first symbol.
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw1_4MHz, SymbolCount = 1 };
            WaveformModel wf = Calc(cfg);

            int fft = 128, cp = 9;
            for (int k = 0; k < cp; k++)
            {
                // sample[k] (the CP) should equal sample[cp + fft - cp + k] (the tail of the block).
                int tail = cp + (fft - cp) + k;
                Assert.Equal(wf.I[k], wf.I[tail], 5);
                Assert.Equal(wf.Q[k], wf.Q[tail], 5);
            }
        }

        [Fact]
        public void Deterministic_for_same_config()
        {
            var cfg = new LteConfig { Bandwidth = LteBandwidth.Bw5MHz, SymbolCount = 7 };
            WaveformModel a = Calc(cfg);
            WaveformModel b = Calc(cfg);
            for (int s = 0; s < a.Length; s++) Assert.Equal(a.I[s], b.I[s], 6);
        }

        [Fact]
        public void Inverse_fft_round_trips_via_forward_transform()
        {
            // IFFT then a manual DFT-forward should recover the spectrum (sanity for Fft.Inverse).
            int n = 8;
            var re = new double[n];
            var im = new double[n];
            re[1] = 1.0; // single tone at bin 1
            var re0 = (double[])re.Clone();
            var im0 = (double[])im.Clone();

            Fft.Inverse(re, im);

            // Forward DFT of the time samples at bin 1 should be ~1, others ~0.
            for (int kBin = 0; kBin < n; kBin++)
            {
                double sr = 0, si = 0;
                for (int t = 0; t < n; t++)
                {
                    double ang = -2 * Math.PI * kBin * t / n;
                    sr += re[t] * Math.Cos(ang) - im[t] * Math.Sin(ang);
                    si += re[t] * Math.Sin(ang) + im[t] * Math.Cos(ang);
                }
                Assert.Equal(re0[kBin], sr, 6);
                Assert.Equal(im0[kBin], si, 6);
            }
        }

        private static WaveformModel Calc(LteConfig cfg)
        {
            var p = new LteFddPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
