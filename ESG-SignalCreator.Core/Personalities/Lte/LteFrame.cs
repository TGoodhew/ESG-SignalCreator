using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Lte
{
    /// <summary>
    /// Builds a structured 3GPP E-UTRA downlink radio-frame (FDD) — the v2 framing for N7624B (#188).
    /// It lays out a proper resource grid over the 10 ms frame (0.5 ms slots, per-symbol cyclic prefix,
    /// normal or extended CP) and places the synchronisation and reference signals at their standard
    /// positions before inverse-FFT'ing each OFDM symbol:
    /// <list type="bullet">
    ///   <item><b>PSS</b> (Zadoff-Chu) on the last symbol of slots 0 and 10, central 62 subcarriers.</item>
    ///   <item><b>SSS</b> (interleaved m-sequences) on the symbol before PSS, central 62 subcarriers.</item>
    ///   <item><b>CRS</b> (cell-specific reference signals, antenna port 0) at the standard time/frequency
    ///         positions with the cell-ID frequency shift.</item>
    ///   <item><b>PDSCH</b>-style QAM data on the remaining resource elements.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// The PSS/SSS/CRS sequences follow 3GPP TS 36.211; positions and the cell-ID dependence are
    /// standards-based. It remains a representative frame, not a fully conformant one: single antenna
    /// port (port 0), no PBCH/PDCCH/PCFICH/PHICH payloads or channel coding, no PDSCH scrambling, and a
    /// symmetric DC-nulled subcarrier layout (matching the shared OFDM engine). MIMO, uplink, HARQ, and
    /// carrier aggregation remain out of scope (tracked in #188).
    /// </remarks>
    internal static class LteFrame
    {
        public static WaveformModel Generate(LteConfig cfg, string name, IProgress<int> progress)
        {
            if (cfg.PhysicalCellId < 0 || cfg.PhysicalCellId > 503)
                throw new InvalidOperationException("PhysicalCellId must be in 0..503.");
            if (cfg.SubframeCount < 1)
                throw new InvalidOperationException("SubframeCount must be at least 1.");

            LteWaveform.LteNumerology(cfg.Bandwidth, out int fft, out int occupied, out int _);
            int nRb = occupied / 12;
            int half = occupied / 2;                 // data subcarriers each side of the (nulled) DC
            bool extended = cfg.CyclicPrefix == LteCyclicPrefix.Extended;
            int symbolsPerSlot = extended ? 6 : 7;

            // Per-symbol cyclic-prefix lengths, scaled from the 2048-FFT reference values.
            int cp0 = (int)Math.Round(160.0 * fft / 2048.0);   // first symbol of each slot (normal CP)
            int cpN = (int)Math.Round(144.0 * fft / 2048.0);   // other symbols (normal CP)
            int cpE = (int)Math.Round(512.0 * fft / 2048.0);   // every symbol (extended CP)

            int nid = cfg.PhysicalCellId;
            int nid2 = nid % 3;
            int nid1 = nid / 3;
            int vShift = nid % 6;
            int nCp = extended ? 0 : 1;

            int slots = cfg.SubframeCount * 2;
            int totalSymbols = slots * symbolsPerSlot;

            // Precompute the total sample count (sum of CP + FFT over every symbol).
            long total = 0;
            for (int i = 0; i < totalSymbols; i++)
                total += CpLen(i % symbolsPerSlot, extended, cp0, cpN, cpE) + fft;

            var outI = new float[total];
            var outQ = new float[total];

            var mapper = new SymbolMapper(cfg.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var symBits = new int[bitsPerSym];

            // Sync-signal sequences depend only on the cell ID, so build them once.
            double[] pssRe = new double[62], pssIm = new double[62];
            Pss(nid2, pssRe, pssIm);
            int[] sssSub0 = Sss(nid1, nid2, subframe5: false);
            int[] sssSub5 = Sss(nid1, nid2, subframe5: true);

            int lPss = symbolsPerSlot - 1;           // last symbol of the slot
            int lSss = symbolsPerSlot - 2;           // symbol before PSS
            int lCrs2 = symbolsPerSlot - 3;          // second CRS symbol (l=4 normal, l=3 extended)

            long pos = 0;
            int reportEvery = Math.Max(1, totalSymbols / 100);
            for (int i = 0; i < totalSymbols; i++)
            {
                int l = i % symbolsPerSlot;
                int ns = (i / symbolsPerSlot) % 20;  // slot number within the 10 ms frame (0..19)

                var re = new double[fft];
                var im = new double[fft];

                // 1) PDSCH data fill on every occupied subcarrier (special signals overwrite below).
                for (int d = 1; d <= half; d++)
                {
                    Place(re, im, fft, +d, MapQam(mapper, bit, symBits, bitsPerSym));
                    Place(re, im, fft, -d, MapQam(mapper, bit, symBits, bitsPerSym));
                }

                // 2) CRS (antenna port 0) on symbols l = 0 and l = symbolsPerSlot-3.
                if (l == 0 || l == lCrs2)
                {
                    int v = l == 0 ? 0 : 3;
                    double[] crsRe, crsIm;
                    Crs(ns, l, nid, nCp, nRb, out crsRe, out crsIm);
                    int mPrime = 0;
                    for (int k = (v + vShift) % 6; k < occupied + 1; k += 6)
                    {
                        int d = k - half;            // DC-centred offset
                        if (d != 0 && d >= -half && d <= half && mPrime < crsRe.Length)
                            Place(re, im, fft, d, crsRe[mPrime], crsIm[mPrime]);
                        if (d != 0) mPrime++;
                    }
                }

                // 3) PSS / SSS on the central 62 subcarriers of slots 0 and 10.
                if (ns == 0 || ns == 10)
                {
                    if (l == lPss)
                        PlaceCentral62(re, im, fft, half, pssRe, pssIm);
                    else if (l == lSss)
                    {
                        int[] s = ns == 0 ? sssSub0 : sssSub5;
                        PlaceCentral62Bpsk(re, im, fft, half, s);
                    }
                }

                Fft.Inverse(re, im);

                int cp = CpLen(l, extended, cp0, cpN, cpE);
                for (int k = 0; k < cp; k++)
                {
                    int src = fft - cp + k;
                    outI[pos + k] = (float)re[src];
                    outQ[pos + k] = (float)im[src];
                }
                for (int k = 0; k < fft; k++)
                {
                    outI[pos + cp + k] = (float)re[k];
                    outQ[pos + cp + k] = (float)im[k];
                }
                pos += cp + fft;

                if (progress != null && (i % reportEvery == 0))
                    progress.Report((int)((long)i * 100 / totalSymbols));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);

            double sampleRate = fft * 15e3;
            return new WaveformModel(outI, outQ, sampleRate, name);
        }

        private static int CpLen(int l, bool extended, int cp0, int cpN, int cpE)
            => extended ? cpE : (l == 0 ? cp0 : cpN);

        // --- resource-element placement ------------------------------------------------

        /// <summary>Place a complex value at DC-centred subcarrier offset d (d != 0) into the FFT bins.</summary>
        private static void Place(double[] re, double[] im, int fft, int d, (double, double) v)
            => Place(re, im, fft, d, v.Item1, v.Item2);

        private static void Place(double[] re, double[] im, int fft, int d, double vr, double vi)
        {
            int bin = d > 0 ? d : fft + d;           // d<0 wraps to the top of the spectrum
            re[bin] = vr; im[bin] = vi;
        }

        /// <summary>Map the central-62 PSS onto subcarriers d = -31..-1 then +1..+31.</summary>
        private static void PlaceCentral62(double[] re, double[] im, int fft, int half, double[] sr, double[] si)
        {
            for (int n = 0; n < 62; n++)
            {
                int d = n < 31 ? n - 31 : n - 30; // n=0..30 -> -31..-1 ; n=31..61 -> +1..+31
                Place(re, im, fft, d, sr[n], si[n]);
            }
        }

        /// <summary>Map the central-62 SSS (BPSK ±1 on I) onto subcarriers d = -31..-1 then +1..+31.</summary>
        private static void PlaceCentral62Bpsk(double[] re, double[] im, int fft, int half, int[] s)
        {
            for (int n = 0; n < 62; n++)
            {
                int d = n < 31 ? n - 31 : n - 30;
                Place(re, im, fft, d, s[n], 0.0);
            }
        }

        private static (double, double) MapQam(SymbolMapper mapper, Func<int> bit, int[] symBits, int bitsPerSym)
        {
            for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
            mapper.Map(symBits, out double di, out double dq);
            return (di, dq);
        }

        // --- 36.211 sequences ----------------------------------------------------------

        /// <summary>Primary synchronisation signal: a length-62 Zadoff-Chu sequence (DC punctured).</summary>
        private static void Pss(int nid2, double[] outRe, double[] outIm)
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

        /// <summary>
        /// Secondary synchronisation signal: the interleaved concatenation of two length-31
        /// m-sequences (36.211 §6.11.2), returning 62 BPSK (±1) values.
        /// </summary>
        private static int[] Sss(int nid1, int nid2, bool subframe5)
        {
            // m0, m1 from N_id_1 (36.211 Table 6.11.2.1-1 formulae).
            int qPrime = nid1 / 30;
            int q = (nid1 + qPrime * (qPrime + 1) / 2) / 30;
            int mPrime = nid1 + q * (q + 1) / 2;
            int m0 = mPrime % 31;
            int m1 = (m0 + mPrime / 31 + 1) % 31;

            int[] sTilde = MSeq(new[] { 0, 0, 0, 0, 1 }, n => (n + 2, n));      // x(i+5)=x(i+2)+x(i)
            int[] cTilde = MSeq(new[] { 0, 0, 0, 0, 1 }, n => (n + 3, n));      // x(i+5)=x(i+3)+x(i)
            int[] zTilde = MSeqZ(new[] { 0, 0, 0, 0, 1 });

            int[] s0 = new int[31], s1 = new int[31], c0 = new int[31], c1 = new int[31], z0 = new int[31], z1 = new int[31];
            for (int n = 0; n < 31; n++)
            {
                s0[n] = sTilde[(n + m0) % 31];
                s1[n] = sTilde[(n + m1) % 31];
                c0[n] = cTilde[(n + nid2) % 31];
                c1[n] = cTilde[(n + nid2 + 3) % 31];
                z0[n] = zTilde[(n + (m0 % 8)) % 31];
                z1[n] = zTilde[(n + (m1 % 8)) % 31];
            }

            var d = new int[62];
            for (int n = 0; n < 31; n++)
            {
                if (!subframe5)
                {
                    d[2 * n] = s0[n] * c0[n];
                    d[2 * n + 1] = s1[n] * c1[n] * z0[n];
                }
                else
                {
                    d[2 * n] = s1[n] * c0[n];
                    d[2 * n + 1] = s0[n] * c1[n] * z1[n];
                }
            }
            return d;
        }

        /// <summary>Length-31 m-sequence s~/c~ as ±1, with a two-tap recurrence x(i+5)=x(i+a)+x(i+b).</summary>
        private static int[] MSeq(int[] init, Func<int, (int, int)> taps)
        {
            var x = new int[31];
            for (int i = 0; i < 5; i++) x[i] = init[i];
            for (int i = 0; i < 26; i++)
            {
                (int a, int b) = taps(i);
                x[i + 5] = (x[a] + x[b]) % 2;
            }
            var s = new int[31];
            for (int i = 0; i < 31; i++) s[i] = 1 - 2 * x[i];
            return s;
        }

        /// <summary>Length-31 m-sequence z~ (four-tap recurrence x(i+5)=x(i+4)+x(i+2)+x(i+1)+x(i)).</summary>
        private static int[] MSeqZ(int[] init)
        {
            var x = new int[31];
            for (int i = 0; i < 5; i++) x[i] = init[i];
            for (int i = 0; i < 26; i++)
                x[i + 5] = (x[i + 4] + x[i + 2] + x[i + 1] + x[i]) % 2;
            var z = new int[31];
            for (int i = 0; i < 31; i++) z[i] = 1 - 2 * x[i];
            return z;
        }

        /// <summary>
        /// Cell-specific reference-signal (CRS) values for one OFDM symbol, antenna port 0 (36.211
        /// §6.10.1). Returns 2·N_RB QPSK values (normalised to unit magnitude).
        /// </summary>
        private static void Crs(int ns, int l, int nid, int nCp, int nRb, out double[] outRe, out double[] outIm)
        {
            const int nRbMax = 110;
            long cinit = 1024L * (7 * (ns + 1) + l + 1) * (2 * nid + 1) + 2 * nid + nCp;
            int need = 2 * (2 * nRbMax);             // c(2m), c(2m+1) for m up to 2*N_RB_max-1
            int[] c = Gold(cinit, need);

            int count = 2 * nRb;
            outRe = new double[count];
            outIm = new double[count];
            double inv = 1.0 / Math.Sqrt(2.0);
            int mOffset = nRbMax - nRb;               // r(m') uses m = m' + (N_RB_max - N_RB)
            for (int mp = 0; mp < count; mp++)
            {
                int m = mp + 2 * mOffset;
                outRe[mp] = inv * (1 - 2 * c[2 * m]);
                outIm[mp] = inv * (1 - 2 * c[2 * m + 1]);
            }
        }

        /// <summary>Length-31 Gold sequence c(n) (36.211 §7.2) with Nc = 1600.</summary>
        private static int[] Gold(long cinit, int length)
        {
            const int Nc = 1600;
            int len = length + Nc + 31;
            var x1 = new int[len];
            var x2 = new int[len];
            x1[0] = 1;
            for (int i = 0; i < 31; i++) x2[i] = (int)((cinit >> i) & 1);
            for (int n = 0; n < len - 31; n++)
            {
                x1[n + 31] = (x1[n + 3] + x1[n]) % 2;
                x2[n + 31] = (x2[n + 3] + x2[n + 2] + x2[n + 1] + x2[n]) % 2;
            }
            var c = new int[length];
            for (int n = 0; n < length; n++) c[n] = (x1[n + Nc] + x2[n + Nc]) % 2;
            return c;
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
