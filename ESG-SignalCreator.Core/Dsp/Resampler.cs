using System;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// Simple sample-rate conversion by linear interpolation (sufficient for P1 previewing).
    /// The first and last input samples are preserved exactly at the output endpoints.
    /// </summary>
    public static class Resampler
    {
        /// <summary>
        /// Resample <paramref name="x"/> to exactly <paramref name="targetLength"/> samples
        /// using linear interpolation. Endpoints are preserved.
        /// </summary>
        public static double[] ResampleLinear(double[] x, int targetLength)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (targetLength < 0) throw new ArgumentOutOfRangeException(nameof(targetLength));

            var y = new double[targetLength];
            if (targetLength == 0) return y;
            int n = x.Length;
            if (n == 0) return y; // nothing to interpolate from; leave zeros

            if (n == 1)
            {
                for (int k = 0; k < targetLength; k++) y[k] = x[0];
                return y;
            }
            if (targetLength == 1)
            {
                y[0] = x[0];
                return y;
            }

            // Map output index k -> input position in [0, n-1], hitting both endpoints.
            double scale = (double)(n - 1) / (targetLength - 1);
            for (int k = 0; k < targetLength; k++)
            {
                double pos = k * scale;
                int i0 = (int)Math.Floor(pos);
                if (i0 >= n - 1)
                {
                    y[k] = x[n - 1];
                    continue;
                }
                double frac = pos - i0;
                y[k] = x[i0] + frac * (x[i0 + 1] - x[i0]);
            }
            return y;
        }

        /// <summary>
        /// Resample complex I/Q to <paramref name="targetLength"/> samples by linear
        /// interpolation of each rail independently. Endpoints are preserved.
        /// </summary>
        public static void ResampleComplexLinear(double[] i, double[] q, int targetLength,
            out double[] oi, out double[] oq)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length)
                throw new ArgumentException("I and Q must have equal length.");
            oi = ResampleLinear(i, targetLength);
            oq = ResampleLinear(q, targetLength);
        }
    }
}
