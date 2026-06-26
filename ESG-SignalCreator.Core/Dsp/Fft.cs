using System;

namespace EsgSignalCreator.Dsp
{
    /// <summary>Minimal in-place radix-2 Cooley-Tukey FFT and spectrum helpers for previewing.</summary>
    public static class Fft
    {
        /// <summary>
        /// Compute a centered magnitude spectrum (in dB, normalized to 0 dB peak) from complex I/Q.
        /// Returns frequency axis (Hz, -fs/2..+fs/2) and magnitude (dB). Input is windowed (Hann)
        /// and zero-padded to the next power of two up to <paramref name="maxBins"/>.
        /// </summary>
        public static void MagnitudeSpectrumDb(double[] i, double[] q, double sampleRateHz,
            out double[] freqHz, out double[] magDb, int maxBins = 8192)
        {
            int n = Math.Min(i.Length, maxBins);
            int size = NextPow2(n);

            var re = new double[size];
            var im = new double[size];

            // Hann window over the first n samples; the rest stay zero (zero-padding).
            for (int k = 0; k < n; k++)
            {
                double wnd = 0.5 - 0.5 * Math.Cos(2 * Math.PI * k / Math.Max(1, n - 1));
                re[k] = i[k] * wnd;
                im[k] = q[k] * wnd;
            }

            Transform(re, im);

            freqHz = new double[size];
            magDb = new double[size];

            double max = double.NegativeInfinity;
            var mag = new double[size];
            for (int k = 0; k < size; k++)
            {
                mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                if (mag[k] > max) max = mag[k];
            }
            if (max <= 0) max = 1;

            // fftshift so DC is centered; convert to dB relative to the peak.
            int half = size / 2;
            for (int k = 0; k < size; k++)
            {
                int src = (k + half) % size;
                double db = 20 * Math.Log10(mag[src] / max + 1e-12);
                if (db < -120) db = -120;
                magDb[k] = db;
                freqHz[k] = (k - half) * sampleRateHz / size;
            }
        }

        private static int NextPow2(int v)
        {
            int p = 1;
            while (p < v) p <<= 1;
            return Math.Max(2, p);
        }

        private static void Transform(double[] re, double[] im)
        {
            int n = re.Length;

            // Bit-reversal permutation.
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double tr = re[i]; re[i] = re[j]; re[j] = tr;
                    double ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wlenRe = Math.Cos(ang);
                double wlenIm = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double wRe = 1, wIm = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k;
                        int b = i + k + len / 2;
                        double uRe = re[a], uIm = im[a];
                        double vRe = re[b] * wRe - im[b] * wIm;
                        double vIm = re[b] * wIm + im[b] * wRe;
                        re[a] = uRe + vRe; im[a] = uIm + vIm;
                        re[b] = uRe - vRe; im[b] = uIm - vIm;
                        double nwRe = wRe * wlenRe - wIm * wlenIm;
                        wIm = wRe * wlenIm + wIm * wlenRe;
                        wRe = nwRe;
                    }
                }
            }
        }
    }
}
