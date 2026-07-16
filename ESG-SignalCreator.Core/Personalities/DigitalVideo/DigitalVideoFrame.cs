using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.DigitalVideo
{
    /// <summary>
    /// Builds a representative DVB-T COFDM signal with the standard **scattered pilots** — the v2
    /// framing for N7623B (#196). On each OFDM symbol, the carriers where <c>k mod 12 == 3·(l mod 4)</c>
    /// carry boosted (4/3) BPSK pilots — so the pilot pattern shifts by 3 subcarriers every symbol and
    /// repeats every 4 symbols — with the remaining carriers carrying QAM data. Pilot values come from
    /// the DVB-T reference PRBS (polynomial X¹¹ + X² + 1).
    /// </summary>
    /// <remarks>
    /// The scattered-pilot positions, the 4/3 boosting, and the reference PRBS follow ETSI EN 300 744.
    /// It is representative: the continual pilots and TPS carriers (fixed-index tables), PRBS energy
    /// dispersal, RS/convolutional coding, and MPEG-TS framing remain out of scope (tracked in #196).
    /// </remarks>
    internal static class DigitalVideoFrame
    {
        private const double PilotBoost = 4.0 / 3.0;

        public static WaveformModel Generate(DigitalVideoConfig cfg, IProgress<int> progress)
        {
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");

            DigitalVideoPersonality.Numerology(cfg.Mode, out int fft, out int occupied);
            int half = occupied / 2;
            int cp = (int)(fft * WimaxFixedPersonality.CpFraction(cfg.GuardInterval));
            double sampleRate = DigitalVideoPersonality.ElementaryRateHz;
            int symLen = cp + fft;

            // DVB-T reference PRBS w_k (poly X^11 + X^2 + 1), one bit per carrier index.
            int[] w = ReferencePrbs(occupied);

            var outI = new float[cfg.SymbolCount * symLen];
            var outQ = new float[cfg.SymbolCount * symLen];
            int pos = 0;

            var mapper = new SymbolMapper(cfg.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var symBits = new int[bitsPerSym];

            int reportEvery = Math.Max(1, cfg.SymbolCount / 100);
            for (int l = 0; l < cfg.SymbolCount; l++)
            {
                double[] re = new double[fft], im = new double[fft];
                int phase = 3 * (l % 4);              // scattered-pilot phase for this symbol

                for (int i = 0; i < occupied; i++)
                {
                    int d = (i < half) ? i - half : i - half + 1; // carrier index -> DC-centred offset
                    if (i % 12 == phase)
                    {
                        double p = PilotBoost * (1 - 2 * w[i]); // boosted BPSK pilot (real)
                        SetBin(re, im, fft, d, p, 0);
                    }
                    else
                    {
                        for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                        mapper.Map(symBits, out double di, out double dq);
                        SetBin(re, im, fft, d, di, dq);
                    }
                }

                Fft.Inverse(re, im);
                for (int k = 0; k < cp; k++) { int src = fft - cp + k; outI[pos] = (float)re[src]; outQ[pos] = (float)im[src]; pos++; }
                for (int k = 0; k < fft; k++) { outI[pos] = (float)re[k]; outQ[pos] = (float)im[k]; pos++; }

                if (progress != null && (l % reportEvery == 0))
                    progress.Report((int)((long)l * 100 / cfg.SymbolCount));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);
            return new WaveformModel(outI, outQ, sampleRate, "DVB-T (pilots)");
        }

        /// <summary>The DVB-T reference sequence w_k as bits (poly X¹¹ + X² + 1, all-ones seed).</summary>
        private static int[] ReferencePrbs(int count)
        {
            var w = new int[count];
            int lfsr = 0x7FF; // 11 ones
            for (int i = 0; i < count; i++)
            {
                int outBit = lfsr & 1;               // the current register output
                w[i] = outBit;
                int fb = ((lfsr >> 10) ^ (lfsr >> 1)) & 1; // X^11 ⊕ X^2
                lfsr = ((lfsr << 1) | fb) & 0x7FF;
            }
            return w;
        }

        private static void SetBin(double[] re, double[] im, int fft, int d, double vr, double vi)
        {
            int bin = d >= 0 ? d : fft + d;
            re[bin] = vr; im[bin] = vi;
        }

        private static void NormalizePeak(float[] i, float[] q)
        {
            double peak = 0.0;
            for (int s = 0; s < i.Length; s++)
            {
                double m = Math.Sqrt((double)i[s] * i[s] + (double)q[s] * q[s]);
                if (m > peak) peak = m;
            }
            if (peak <= 0.0) return;
            double scale = 1.0 / peak;
            for (int s = 0; s < i.Length; s++) { i[s] = (float)(i[s] * scale); q[s] = (float)(q[s] * scale); }
        }
    }
}
