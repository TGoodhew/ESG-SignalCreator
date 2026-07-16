using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Wlan
{
    /// <summary>
    /// Builds a representative 802.11a/g <b>PPDU</b> at 20 MHz (64-point FFT, 20 MHz sample rate) — the
    /// v2 framing for N7617B (#191). It optionally prepends the <b>L-LTF</b> training field (two long
    /// training symbols with a double guard interval) and then emits data OFDM symbols carrying the four
    /// <b>pilot subcarriers</b> (−21, −7, +7, +21) with the standard polarity, using a selectable data
    /// <b>guard interval</b> (long 0.8 µs / short 0.4 µs).
    /// </summary>
    /// <remarks>
    /// The L-LTF sequence and pilot subcarrier positions/polarity follow IEEE 802.11. It is a
    /// representative PPDU, not fully conformant: the L-STF and L-SIG fields, BCC/LDPC channel coding and
    /// interleaving, MAC framing, MIMO, and the wider (40/80/160 MHz) bandwidths are out of scope
    /// (tracked in #191). The pilot polarity uses the standard scrambler polynomial (x⁷+x⁴+1).
    /// </remarks>
    internal static class WlanPpdu
    {
        private const int FftSize = 64;
        private const double SampleRateHz = 20e6;
        private const int CpLong = 16;   // 0.8 µs
        private const int CpShort = 8;   // 0.4 µs
        private const int Gi2 = 32;      // L-LTF double guard interval (1.6 µs)

        // Pilot subcarriers and their base polarity (IEEE 802.11, 20 MHz).
        private static readonly int[] PilotSubcarriers = { -21, -7, 7, 21 };
        private static readonly int[] PilotBase = { 1, 1, 1, -1 };

        // L-LTF frequency-domain sequence for subcarriers -26..+26 (index 26 = DC = 0).
        private static readonly int[] LtfSeq =
        {
            1, 1, -1, -1, 1, 1, -1, 1, -1, 1, 1, 1, 1, 1, 1, -1, -1, 1, 1, -1, 1, -1, 1, 1, 1, 1,
            0,
            1, -1, -1, 1, 1, -1, 1, -1, 1, -1, -1, -1, -1, -1, 1, 1, -1, -1, 1, -1, 1, -1, 1, 1, 1, 1
        };

        public static WaveformModel Generate(WlanConfig cfg, IProgress<int> progress)
        {
            if (cfg.Bandwidth != WlanBandwidth.Bw20MHz)
                throw new InvalidOperationException("Frame-structured WLAN currently supports 20 MHz only.");
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");

            int cpData = cfg.GuardInterval == WlanGuardInterval.Short ? CpShort : CpLong;
            int preambleLen = cfg.IncludeLtfPreamble ? Gi2 + 2 * FftSize : 0;
            int n = preambleLen + cfg.SymbolCount * (cpData + FftSize);

            var outI = new float[n];
            var outQ = new float[n];
            int pos = 0;

            // L-LTF: [double-GI (last 32 of the LTF symbol)] [LTF] [LTF].
            if (cfg.IncludeLtfPreamble)
            {
                double[] lr = new double[FftSize], li = new double[FftSize];
                for (int k = -26; k <= 26; k++)
                {
                    int v = LtfSeq[k + 26];
                    if (v != 0) SetBin(lr, li, k, v, 0);
                }
                Fft.Inverse(lr, li);
                for (int k = 0; k < Gi2; k++) { int s = FftSize - Gi2 + k; outI[pos] = (float)lr[s]; outQ[pos] = (float)li[s]; pos++; }
                for (int rep = 0; rep < 2; rep++)
                    for (int k = 0; k < FftSize; k++) { outI[pos] = (float)lr[k]; outQ[pos] = (float)li[k]; pos++; }
            }

            var mapper = new SymbolMapper(cfg.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var symBits = new int[bitsPerSym];
            int lfsr = 0x7F; // 7-bit scrambler LFSR, all-ones seed

            int reportEvery = Math.Max(1, cfg.SymbolCount / 100);
            for (int s = 0; s < cfg.SymbolCount; s++)
            {
                double[] re = new double[FftSize], im = new double[FftSize];
                int polarity = NextPolarity(ref lfsr);

                for (int k = -26; k <= 26; k++)
                {
                    if (k == 0) continue;                 // DC null
                    int pilotIdx = PilotIndex(k);
                    if (pilotIdx >= 0)
                    {
                        SetBin(re, im, k, PilotBase[pilotIdx] * polarity, 0);
                    }
                    else
                    {
                        for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                        mapper.Map(symBits, out double di, out double dq);
                        SetBin(re, im, k, di, dq);
                    }
                }

                Fft.Inverse(re, im);
                for (int k = 0; k < cpData; k++) { int src = FftSize - cpData + k; outI[pos] = (float)re[src]; outQ[pos] = (float)im[src]; pos++; }
                for (int k = 0; k < FftSize; k++) { outI[pos] = (float)re[k]; outQ[pos] = (float)im[k]; pos++; }

                if (progress != null && (s % reportEvery == 0))
                    progress.Report((int)((long)s * 100 / cfg.SymbolCount));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);
            return new WaveformModel(outI, outQ, SampleRateHz, "802.11 WLAN PPDU");
        }

        /// <summary>Index into <see cref="PilotSubcarriers"/> for subcarrier k, or -1 if k is not a pilot.</summary>
        private static int PilotIndex(int k)
        {
            for (int i = 0; i < PilotSubcarriers.Length; i++) if (PilotSubcarriers[i] == k) return i;
            return -1;
        }

        /// <summary>Place a complex value at subcarrier k (may be negative) into the FFT bins.</summary>
        private static void SetBin(double[] re, double[] im, int k, double vr, double vi)
        {
            int bin = k >= 0 ? k : FftSize + k;
            re[bin] = vr; im[bin] = vi;
        }

        /// <summary>Next ±1 pilot polarity from the standard scrambler LFSR (poly x⁷+x⁴+1).</summary>
        private static int NextPolarity(ref int lfsr)
        {
            int fb = ((lfsr >> 6) ^ (lfsr >> 3)) & 1;
            lfsr = ((lfsr << 1) | fb) & 0x7F;
            return 1 - 2 * fb;
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
