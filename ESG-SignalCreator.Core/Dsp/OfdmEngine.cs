using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// A shared OFDM baseband generator for the OFDM-family personalities (LTE, 802.11 WLAN,
    /// 802.16 WiMAX, T-DMB, digital video). It builds a stream of OFDM symbols: QAM data is placed
    /// on the occupied subcarriers (DC nulled, symmetric guard bands), inverse-FFT'd, and given a
    /// cyclic prefix. Sample rate = FFT size × subcarrier spacing.
    /// </summary>
    /// <remarks>
    /// This is a generic OFDM waveform with the right numerology (FFT size, subcarrier spacing, CP,
    /// occupied carriers, modulation) — enough for occupied-bandwidth, PAPR/CCDF, and spectral-shape
    /// checks. It is not a standards-compliant frame: no pilots/reference signals, preambles/sync,
    /// channel coding, or frame structure. Those are per-standard deferred items.
    /// </remarks>
    public static class OfdmEngine
    {
        /// <summary>Parameters for one OFDM waveform.</summary>
        public sealed class Params
        {
            public int FftSize = 2048;
            public int CyclicPrefix = 144;
            public int OccupiedCarriers = 1200; // data subcarriers (excludes DC); must be even
            public int SymbolCount = 14;
            public double SubcarrierSpacingHz = 15e3;
            public Modulation Modulation = Modulation.QAM16;
            public DataSource Data = DataSource.PN9;
            public string Name = "OFDM";
        }

        /// <summary>Generate the OFDM waveform described by <paramref name="p"/>.</summary>
        public static WaveformModel Generate(Params p, IProgress<int> progress)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            if (p.FftSize < 2 || (p.FftSize & (p.FftSize - 1)) != 0)
                throw new InvalidOperationException("FftSize must be a power of two >= 2.");
            if (p.CyclicPrefix < 0 || p.CyclicPrefix >= p.FftSize)
                throw new InvalidOperationException("CyclicPrefix must be in [0, FftSize).");
            if (p.OccupiedCarriers < 2 || (p.OccupiedCarriers & 1) != 0)
                throw new InvalidOperationException("OccupiedCarriers must be a positive even number.");
            if (p.OccupiedCarriers > p.FftSize - 1)
                throw new InvalidOperationException("OccupiedCarriers must be <= FftSize - 1 (DC is nulled).");
            if (p.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");
            if (p.SubcarrierSpacingHz <= 0)
                throw new InvalidOperationException("SubcarrierSpacingHz must be positive.");

            int fft = p.FftSize;
            int cp = p.CyclicPrefix;
            int half = p.OccupiedCarriers / 2;
            int symLen = fft + cp;
            int n = p.SymbolCount * symLen;

            var mapper = new SymbolMapper(p.Modulation);
            int bitsPerSym = mapper.BitsPerSymbol;
            Func<int> bit = Prbs.CreateBitGenerator(p.Data);
            var symBits = new int[bitsPerSym];

            var outI = new float[n];
            var outQ = new float[n];

            int reportEvery = Math.Max(1, p.SymbolCount / 100);
            for (int s = 0; s < p.SymbolCount; s++)
            {
                var re = new double[fft];
                var im = new double[fft];

                // Fill the occupied subcarriers: +1..+half at bins 1..half, -1..-half at bins fft-1..fft-half.
                for (int c = 1; c <= half; c++)
                {
                    for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                    mapper.Map(symBits, out double di, out double dq);
                    re[c] = di; im[c] = dq;                  // positive frequency

                    for (int b = 0; b < bitsPerSym; b++) symBits[b] = bit();
                    mapper.Map(symBits, out double di2, out double dq2);
                    re[fft - c] = di2; im[fft - c] = dq2;    // negative frequency
                }

                Fft.Inverse(re, im); // time-domain OFDM symbol (length fft)

                int outBase = s * symLen;
                // Cyclic prefix: copy the last cp samples of the symbol ahead of it.
                for (int k = 0; k < cp; k++)
                {
                    int src = fft - cp + k;
                    outI[outBase + k] = (float)re[src];
                    outQ[outBase + k] = (float)im[src];
                }
                for (int k = 0; k < fft; k++)
                {
                    outI[outBase + cp + k] = (float)re[k];
                    outQ[outBase + cp + k] = (float)im[k];
                }

                if (progress != null && (s % reportEvery == 0))
                    progress.Report((int)((long)s * 100 / p.SymbolCount));
            }

            // Normalize peak vector magnitude to 1.0.
            double peak = 0.0;
            for (int k = 0; k < n; k++)
            {
                double m = Math.Sqrt((double)outI[k] * outI[k] + (double)outQ[k] * outQ[k]);
                if (m > peak) peak = m;
            }
            if (peak > 0.0)
            {
                double scale = 1.0 / peak;
                for (int k = 0; k < n; k++) { outI[k] = (float)(outI[k] * scale); outQ[k] = (float)(outQ[k] * scale); }
            }

            progress?.Report(100);

            double sampleRate = p.FftSize * p.SubcarrierSpacingHz;
            return new WaveformModel(outI, outQ, sampleRate, p.Name);
        }
    }
}
