using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Tdmb
{
    /// <summary>
    /// Builds a representative DAB / T-DMB transmission frame — the v2 framing for N7616B (#195). It
    /// emits the DAB synchronisation channel (a **null symbol** of silence for frame sync, then a
    /// **phase-reference symbol** carrying a known per-carrier phase) followed by data symbols that are
    /// **differentially encoded (DQPSK)** — each active carrier's phase accumulates the QPSK data delta
    /// from the previous symbol, referenced to the phase-reference symbol.
    /// </summary>
    /// <remarks>
    /// The null + phase-reference symbols and the differential (DQPSK) encoding follow the DAB frame
    /// structure (ETSI EN 300 401). It is representative: the exact phase-reference sequence (the DAB
    /// time-frequency phase-reference table), the FIC/MSC multiplex, the TII, and convolutional coding
    /// remain out of scope (tracked in #195). All modes keep the 2.048 MHz signal bandwidth.
    /// </remarks>
    internal static class TdmbFrame
    {
        public static WaveformModel Generate(TdmbConfig cfg, IProgress<int> progress)
        {
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");

            TdmbPersonality.Numerology(cfg.Mode, out int fft, out int occupied, out int cp);
            int half = occupied / 2;
            double sampleRate = TdmbPersonality.SignalBandwidthHz;
            int symLen = cp + fft;

            // Frame = null symbol + phase-reference symbol + data symbols.
            int totalSymbols = 2 + cfg.SymbolCount;
            int n = totalSymbols * symLen;
            var outI = new float[n];
            var outQ = new float[n];
            int pos = 0;

            // 1) Null symbol — a full symbol period of silence (frame synchronisation).
            pos += symLen;

            // 2) Phase-reference symbol — a fixed known per-carrier phase (the DQPSK reference).
            var theta = new double[occupied];      // running phase per active carrier
            int prbs = 0x7FF;
            {
                double[] re = new double[fft], im = new double[fft];
                int i = 0;
                for (int d = -half; d <= half; d++)
                {
                    if (d == 0) continue;
                    double ph = (Math.PI / 2.0) * (2 * NextBit(ref prbs) + NextBit(ref prbs)); // one of 4 phases
                    theta[i] = ph;
                    SetBin(re, im, fft, d, Math.Cos(ph), Math.Sin(ph));
                    i++;
                }
                EmitSymbol(re, im, fft, cp, outI, outQ, ref pos);
            }

            // 3) Data symbols — DQPSK: phase[k] += QPSK delta from two payload bits.
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            int reportEvery = Math.Max(1, cfg.SymbolCount / 100);
            for (int s = 0; s < cfg.SymbolCount; s++)
            {
                double[] re = new double[fft], im = new double[fft];
                int i = 0;
                for (int d = -half; d <= half; d++)
                {
                    if (d == 0) continue;
                    int sym = 2 * bit() + bit();                 // 0..3
                    theta[i] += sym * (Math.PI / 2.0);           // differential (DQPSK) accumulation
                    SetBin(re, im, fft, d, Math.Cos(theta[i]), Math.Sin(theta[i]));
                    i++;
                }
                EmitSymbol(re, im, fft, cp, outI, outQ, ref pos);

                if (progress != null && (s % reportEvery == 0))
                    progress.Report((int)((long)s * 100 / cfg.SymbolCount));
            }

            NormalizePeak(outI, outQ);
            progress?.Report(100);
            return new WaveformModel(outI, outQ, sampleRate, "T-DMB frame");
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
