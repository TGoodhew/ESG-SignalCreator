using System;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// Standard window functions. Each returns an <paramref name="n"/>-length array.
    /// Symmetric definitions (denominator n-1) are used so the endpoints reach the
    /// classic boundary values (e.g. Hann endpoints == 0, midpoint == 1).
    /// </summary>
    public static class Windowing
    {
        /// <summary>Rectangular (boxcar) window: all ones.</summary>
        public static double[] Rectangular(int n)
        {
            var w = AllocOrTrivial(n, out bool done);
            if (done) return w;
            for (int k = 0; k < n; k++) w[k] = 1.0;
            return w;
        }

        /// <summary>Hann (raised-cosine) window. Endpoints 0, midpoint 1.</summary>
        public static double[] Hann(int n)
        {
            var w = AllocOrTrivial(n, out bool done);
            if (done) return w;
            double denom = n - 1;
            for (int k = 0; k < n; k++)
                w[k] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * k / denom);
            return w;
        }

        /// <summary>Hamming window. Endpoints 0.08, midpoint 1.</summary>
        public static double[] Hamming(int n)
        {
            var w = AllocOrTrivial(n, out bool done);
            if (done) return w;
            double denom = n - 1;
            for (int k = 0; k < n; k++)
                w[k] = 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * k / denom);
            return w;
        }

        /// <summary>Blackman window (classic coefficients 0.42, 0.5, 0.08).</summary>
        public static double[] Blackman(int n)
        {
            var w = AllocOrTrivial(n, out bool done);
            if (done) return w;
            double denom = n - 1;
            for (int k = 0; k < n; k++)
            {
                double x = 2.0 * Math.PI * k / denom;
                w[k] = 0.42 - 0.5 * Math.Cos(x) + 0.08 * Math.Cos(2.0 * x);
            }
            return w;
        }

        // Handles n<=0 (empty) and n==1 (single sample == 1) which would divide by zero.
        private static double[] AllocOrTrivial(int n, out bool done)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (n == 0) { done = true; return new double[0]; }
            if (n == 1) { done = true; return new[] { 1.0 }; }
            done = false;
            return new double[n];
        }
    }
}
