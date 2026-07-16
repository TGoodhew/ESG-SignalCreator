using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.WimaxMobile
{
    /// <summary>
    /// Builds a representative mobile-WiMAX (IEEE 802.16e) downlink OFDMA frame — the v2 framing for
    /// N7615B (#193). It optionally prepends the DL **preamble** (a BPSK PN carried on every 3rd used
    /// subcarrier — the OFDMA-preamble subcarrier spacing) and emits data OFDM symbols that carry a
    /// **DL-PUSC pilot pattern** (two pilots per 14-subcarrier cluster, at cluster positions 4 and 8).
    /// </summary>
    /// <remarks>
    /// The preamble's every-3rd-subcarrier layout and the 14-subcarrier DL-PUSC cluster with two pilots
    /// follow the IEEE 802.16e structure, but this is a representative frame: the exact preamble PN
    /// series per IDcell/segment, the full PUSC/FUSC/AMC subchannel permutation, FCH/DL-MAP/UL-MAP,
    /// MIMO, and CTC/CC channel coding remain out of scope (tracked in #193).
    /// </remarks>
    internal static class WimaxMobileFrame
    {
        private const double SpacingHz = WimaxMobilePersonality.SubcarrierSpacingHz;
        private const int ClusterSize = 14;               // DL-PUSC cluster width
        private static readonly int[] ClusterPilotPos = { 4, 8 }; // pilot positions within a cluster

        public static WaveformModel Generate(WimaxMobileConfig cfg, IProgress<int> progress)
        {
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");

            WimaxMobilePersonality.Numerology(cfg.FftSize, out int fft, out int occupied);
            int half = occupied / 2;
            double sampleRate = fft * SpacingHz;
            int cp = (int)(fft * WimaxFixedPersonality.CpFraction(cfg.CyclicPrefixRatio));
            int symLen = cp + fft;

            int preambleSymbols = cfg.IncludePreamble ? 1 : 0;
            int n = (preambleSymbols + cfg.SymbolCount) * symLen;
            var outI = new float[n];
            var outQ = new float[n];
            int pos = 0;

            int prbs = 0x7FF; // 11-bit PRBS (1+X^9+X^11), all-ones seed

            if (cfg.IncludePreamble)
            {
                double[] re = new double[fft], im = new double[fft];
                // Every 3rd used subcarrier (by index) carries a BPSK PN value.
                int i = 0;
                for (int d = -half; d <= half; d++)
                {
                    if (d == 0) continue;
                    if (i % 3 == 0) SetBin(re, im, fft, d, 1 - 2 * NextBit(ref prbs), 0);
                    i++;
                }
                EmitSymbol(re, im, fft, cp, outI, outQ, ref pos);
            }

            var mapper = new SymbolMapper(cfg.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var symBits = new int[bitsPerSym];

            int reportEvery = Math.Max(1, cfg.SymbolCount / 100);
            for (int s = 0; s < cfg.SymbolCount; s++)
            {
                double[] re = new double[fft], im = new double[fft];
                int pilot = 1 - 2 * NextBit(ref prbs); // one BPSK pilot polarity per symbol

                int i = 0;
                for (int d = -half; d <= half; d++)
                {
                    if (d == 0) continue;
                    if (IsClusterPilot(i))
                    {
                        SetBin(re, im, fft, d, pilot, 0);
                    }
                    else
                    {
                        for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                        mapper.Map(symBits, out double di, out double dq);
                        SetBin(re, im, fft, d, di, dq);
                    }
                    i++;
                }

                EmitSymbol(re, im, fft, cp, outI, outQ, ref pos);

                if (progress != null && (s % reportEvery == 0))
                    progress.Report((int)((long)s * 100 / cfg.SymbolCount));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);
            return new WaveformModel(outI, outQ, sampleRate, "802.16e Mobile WiMAX frame");
        }

        private static bool IsClusterPilot(int usedIndex)
        {
            int within = usedIndex % ClusterSize;
            for (int i = 0; i < ClusterPilotPos.Length; i++) if (ClusterPilotPos[i] == within) return true;
            return false;
        }

        private static void EmitSymbol(double[] re, double[] im, int fft, int cp, float[] outI, float[] outQ, ref int pos)
        {
            Fft.Inverse(re, im);
            for (int k = 0; k < cp; k++) { int src = fft - cp + k; outI[pos] = (float)re[src]; outQ[pos] = (float)im[src]; pos++; }
            for (int k = 0; k < fft; k++) { outI[pos] = (float)re[k]; outQ[pos] = (float)im[k]; pos++; }
        }

        private static void SetBin(double[] re, double[] im, int fft, int d, double vr, double vi)
        {
            int bin = d >= 0 ? d : fft + d;
            re[bin] = vr; im[bin] = vi;
        }

        private static int NextBit(ref int lfsr)
        {
            int fb = ((lfsr >> 10) ^ (lfsr >> 8)) & 1;
            lfsr = ((lfsr << 1) | fb) & 0x7FF;
            return fb;
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
