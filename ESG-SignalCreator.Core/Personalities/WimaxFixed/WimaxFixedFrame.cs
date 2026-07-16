using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.WimaxFixed
{
    /// <summary>
    /// Builds a representative fixed-WiMAX (IEEE 802.16-2004) 256-FFT OFDM frame — the v2 framing for
    /// N7613A (#192). It places the exact used-subcarrier map (192 data + 8 pilots at ±13/±38/±63/±88,
    /// DC and guard bands nulled), modulates the pilots with the standard pilot PRBS (poly 1+X⁹+X¹¹), and
    /// optionally prepends a downlink preamble symbol (energy on every 4th subcarrier → four identical
    /// quarter-symbol repetitions in time, the hallmark of the WiMAX preamble).
    /// </summary>
    /// <remarks>
    /// Pilot positions and the pilot-modulation PRBS follow IEEE 802.16-2004. The preamble is a
    /// representative every-4th-subcarrier symbol, not the exact two-symbol long preamble (P_ALL/P_EVEN);
    /// the FCH, DL-MAP/UL-MAP, DCD/UCD, RS-CC channel coding, and MAC framing remain out of scope
    /// (tracked in #192).
    /// </remarks>
    internal static class WimaxFixedFrame
    {
        private const int FftSize = 256;
        // Used subcarriers span -100..+100 excluding DC; pilots sit at these eight indices.
        private static readonly int[] PilotSubcarriers = { -88, -63, -38, -13, 13, 38, 63, 88 };

        public static WaveformModel Generate(WimaxFixedConfig cfg, IProgress<int> progress)
        {
            if (cfg.ChannelBandwidthHz <= 0)
                throw new InvalidOperationException("ChannelBandwidthHz must be positive.");
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");

            double sampleRate = cfg.ChannelBandwidthHz * 8.0 / 7.0;
            double spacing = sampleRate / FftSize;
            int cp = (int)(FftSize * WimaxFixedPersonality.CpFraction(cfg.CyclicPrefixRatio));
            int symLen = cp + FftSize;

            int preambleSymbols = cfg.IncludePreamble ? 1 : 0;
            int n = (preambleSymbols + cfg.SymbolCount) * symLen;
            var outI = new float[n];
            var outQ = new float[n];
            int pos = 0;

            int prbs = 0x7FF; // 11-bit pilot/preamble PRBS, all-ones seed

            if (cfg.IncludePreamble)
            {
                double[] re = new double[FftSize], im = new double[FftSize];
                // Every 4th used subcarrier carries a BPSK PRBS value; the rest are null.
                for (int k = -100; k <= 100; k++)
                {
                    if (k == 0 || (k % 4) != 0) continue;
                    SetBin(re, im, k, 1 - 2 * NextBit(ref prbs), 0);
                }
                EmitSymbol(re, im, cp, outI, outQ, ref pos);
            }

            var mapper = new SymbolMapper(cfg.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var symBits = new int[bitsPerSym];

            int reportEvery = Math.Max(1, cfg.SymbolCount / 100);
            for (int s = 0; s < cfg.SymbolCount; s++)
            {
                double[] re = new double[FftSize], im = new double[FftSize];
                int pilot = 1 - 2 * NextBit(ref prbs); // one BPSK pilot polarity per symbol

                for (int k = -100; k <= 100; k++)
                {
                    if (k == 0) continue;                 // DC null
                    if (IsPilot(k))
                    {
                        SetBin(re, im, k, pilot, 0);
                    }
                    else
                    {
                        for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                        mapper.Map(symBits, out double di, out double dq);
                        SetBin(re, im, k, di, dq);
                    }
                }

                EmitSymbol(re, im, cp, outI, outQ, ref pos);

                if (progress != null && (s % reportEvery == 0))
                    progress.Report((int)((long)s * 100 / cfg.SymbolCount));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);
            return new WaveformModel(outI, outQ, sampleRate, "802.16-2004 WiMAX frame");
        }

        private static void EmitSymbol(double[] re, double[] im, int cp, float[] outI, float[] outQ, ref int pos)
        {
            Fft.Inverse(re, im);
            for (int k = 0; k < cp; k++) { int src = FftSize - cp + k; outI[pos] = (float)re[src]; outQ[pos] = (float)im[src]; pos++; }
            for (int k = 0; k < FftSize; k++) { outI[pos] = (float)re[k]; outQ[pos] = (float)im[k]; pos++; }
        }

        private static bool IsPilot(int k)
        {
            for (int i = 0; i < PilotSubcarriers.Length; i++) if (PilotSubcarriers[i] == k) return true;
            return false;
        }

        private static void SetBin(double[] re, double[] im, int k, double vr, double vi)
        {
            int bin = k >= 0 ? k : FftSize + k;
            re[bin] = vr; im[bin] = vi;
        }

        /// <summary>Next bit of the 802.16 pilot PRBS (poly 1 + X⁹ + X¹¹, 11-stage LFSR).</summary>
        private static int NextBit(ref int lfsr)
        {
            int fb = ((lfsr >> 10) ^ (lfsr >> 8)) & 1; // X¹¹ ⊕ X⁹
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
