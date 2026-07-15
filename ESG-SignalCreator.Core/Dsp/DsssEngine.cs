using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// A shared direct-sequence spread-spectrum (DSSS) baseband generator for the CDMA-family
    /// personalities (W-CDMA / HSPA, cdma2000, TD-SCDMA, S-DMB). It builds a single code channel:
    /// data symbols (QPSK/QAM) are spread by an OVSF (Walsh) code, optionally complex-scrambled by a
    /// PN sequence, upsampled, and root-raised-cosine pulse-shaped at the chip rate.
    /// </summary>
    /// <remarks>
    /// This is a representative single-channel model, not a standards-compliant multi-code downlink.
    /// It captures the defining structure — spreading + scrambling + chip-rate RRC — so the resulting
    /// signal has the right chip rate, occupied bandwidth, and spectral shape for bench checks.
    /// </remarks>
    public static class DsssEngine
    {
        /// <summary>Parameters for one DSSS waveform.</summary>
        public sealed class Params
        {
            public double ChipRateHz = 3.84e6;
            public int SamplesPerChip = 4;
            public int SymbolCount = 256;
            public int SpreadingFactor = 16;
            public int OvsfIndex = 1;
            public Modulation Modulation = Modulation.QPSK;
            public double RrcBeta = 0.22;
            public int RrcSpanChips = 6;
            public bool Scramble = true;
            public int ScrambleSeed = 1;
            public DataSource Data = DataSource.PN9;
            public string Name = "DSSS";
        }

        /// <summary>Generate the DSSS waveform described by <paramref name="p"/>.</summary>
        public static WaveformModel Generate(Params p, IProgress<int> progress)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (p.ChipRateHz <= 0) throw new InvalidOperationException("ChipRateHz must be positive.");
            if (p.SamplesPerChip < 1) throw new InvalidOperationException("SamplesPerChip must be at least 1.");
            if (p.SymbolCount < 1) throw new InvalidOperationException("SymbolCount must be at least 1.");
            if (p.SpreadingFactor < 1) throw new InvalidOperationException("SpreadingFactor must be at least 1.");
            if ((p.SpreadingFactor & (p.SpreadingFactor - 1)) != 0)
                throw new InvalidOperationException("SpreadingFactor must be a power of two (OVSF).");
            if (p.OvsfIndex < 0 || p.OvsfIndex >= p.SpreadingFactor)
                throw new InvalidOperationException("OvsfIndex must be in [0, SpreadingFactor).");
            if (p.RrcBeta < 0 || p.RrcBeta > 1) throw new InvalidOperationException("RrcBeta must be in [0,1].");

            progress?.Report(5);

            int sf = p.SpreadingFactor;
            int[] ovsf = OvsfCode(sf, p.OvsfIndex);

            var mapper = new SymbolMapper(p.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(p.Data);

            int chipCount = p.SymbolCount * sf;
            var chipI = new double[chipCount];
            var chipQ = new double[chipCount];

            var scr = p.Scramble ? new Random(p.ScrambleSeed) : null;
            var symBits = new int[bitsPerSym];

            for (int s = 0; s < p.SymbolCount; s++)
            {
                for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                mapper.Map(symBits, out double si, out double sq);

                for (int c = 0; c < sf; c++)
                {
                    int idx = s * sf + c;
                    double code = ovsf[c]; // ±1
                    double ci = si * code;
                    double cq = sq * code;

                    if (scr != null)
                    {
                        // Simple magnitude-preserving complex scramble: independent ±1 on each rail.
                        double sgnI = scr.Next(0, 2) == 0 ? 1.0 : -1.0;
                        double sgnQ = scr.Next(0, 2) == 0 ? 1.0 : -1.0;
                        ci *= sgnI;
                        cq *= sgnQ;
                    }

                    chipI[idx] = ci;
                    chipQ[idx] = cq;
                }
            }

            progress?.Report(40);

            // Upsample chips to SamplesPerChip and RRC pulse-shape.
            int spc = p.SamplesPerChip;
            int n = chipCount * spc;
            var upI = new double[n];
            var upQ = new double[n];
            for (int c = 0; c < chipCount; c++)
            {
                upI[c * spc] = chipI[c]; // zero-stuff
                upQ[c * spc] = chipQ[c];
            }

            double[] taps = Fir.RootRaisedCosine(p.RrcBeta, spc, p.RrcSpanChips);
            Fir.ApplyComplex(upI, upQ, taps, out double[] fi, out double[] fq);

            progress?.Report(80);

            // Normalize peak vector magnitude to 1.0.
            double peak = 0.0;
            for (int k = 0; k < n; k++)
            {
                double m = Math.Sqrt(fi[k] * fi[k] + fq[k] * fq[k]);
                if (m > peak) peak = m;
            }
            var i = new float[n];
            var q = new float[n];
            double scale = peak > 0 ? 1.0 / peak : 1.0;
            for (int k = 0; k < n; k++)
            {
                i[k] = (float)(fi[k] * scale);
                q[k] = (float)(fq[k] * scale);
            }

            progress?.Report(100);

            double sampleRate = p.ChipRateHz * spc;
            return new WaveformModel(i, q, sampleRate, p.Name);
        }

        /// <summary>
        /// The OVSF (Walsh–Hadamard) code of length <paramref name="length"/> (a power of two) at
        /// <paramref name="index"/>, as ±1 chips. Row 0 is the all-ones code.
        /// </summary>
        public static int[] OvsfCode(int length, int index)
        {
            if (length < 1 || (length & (length - 1)) != 0)
                throw new ArgumentException("length must be a power of two.", nameof(length));
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var code = new int[length];
            for (int c = 0; c < length; c++)
            {
                // Walsh-Hadamard element sign = (-1)^popcount(index & c).
                int bits = index & c;
                int parity = 0;
                while (bits != 0) { parity ^= 1; bits &= bits - 1; }
                code[c] = parity == 0 ? 1 : -1;
            }
            return code;
        }
    }
}
