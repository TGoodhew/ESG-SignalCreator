using System;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// FIR pulse-shaping filter design and application.
    ///
    /// Tap-count convention: a filter spanning <paramref name="spanSymbols"/> symbols at
    /// <paramref name="samplesPerSymbol"/> samples/symbol has
    /// <c>samplesPerSymbol * spanSymbols + 1</c> taps. The +1 yields an odd length so the
    /// filter has a single centre tap and is exactly symmetric about it (linear phase).
    /// </summary>
    public static class Fir
    {
        /// <summary>
        /// Root-raised-cosine filter taps. Normalized so the sum of taps (DC gain) == 1.
        /// </summary>
        /// <param name="beta">Roll-off factor in [0, 1].</param>
        public static double[] RootRaisedCosine(double beta, int samplesPerSymbol, int spanSymbols)
        {
            ValidateArgs(beta, samplesPerSymbol, spanSymbols);
            int taps = samplesPerSymbol * spanSymbols + 1;
            var h = new double[taps];
            int mid = taps / 2;
            double sps = samplesPerSymbol;

            for (int k = 0; k < taps; k++)
            {
                double t = (k - mid) / sps; // time in symbol periods
                h[k] = RrcImpulse(t, beta);
            }
            NormalizeSum(h);
            return h;
        }

        /// <summary>
        /// Raised-cosine filter taps. Normalized so the sum of taps (DC gain) == 1.
        /// </summary>
        /// <param name="beta">Roll-off factor in [0, 1].</param>
        public static double[] RaisedCosine(double beta, int samplesPerSymbol, int spanSymbols)
        {
            ValidateArgs(beta, samplesPerSymbol, spanSymbols);
            int taps = samplesPerSymbol * spanSymbols + 1;
            var h = new double[taps];
            int mid = taps / 2;
            double sps = samplesPerSymbol;

            for (int k = 0; k < taps; k++)
            {
                double t = (k - mid) / sps;
                h[k] = RcImpulse(t, beta);
            }
            NormalizeSum(h);
            return h;
        }

        /// <summary>
        /// Gaussian pulse-shaping filter taps. Normalized to unit sum.
        /// </summary>
        /// <param name="bt">Bandwidth-time product (e.g. 0.3 for GSM).</param>
        public static double[] Gaussian(double bt, int samplesPerSymbol, int spanSymbols)
        {
            if (bt <= 0.0) throw new ArgumentOutOfRangeException(nameof(bt), "BT must be positive.");
            if (samplesPerSymbol < 1) throw new ArgumentOutOfRangeException(nameof(samplesPerSymbol));
            if (spanSymbols < 1) throw new ArgumentOutOfRangeException(nameof(spanSymbols));

            int taps = samplesPerSymbol * spanSymbols + 1;
            var h = new double[taps];
            int mid = taps / 2;
            double sps = samplesPerSymbol;

            // h(t) = exp( -(t^2) / (2*sigma^2) ), with t in symbol periods.
            // sigma = sqrt(ln 2) / (2*pi*BT).
            double sigma = Math.Sqrt(Math.Log(2.0)) / (2.0 * Math.PI * bt);
            double twoSigmaSq = 2.0 * sigma * sigma;

            for (int k = 0; k < taps; k++)
            {
                double t = (k - mid) / sps;
                h[k] = Math.Exp(-(t * t) / twoSigmaSq);
            }
            NormalizeSum(h);
            return h;
        }

        /// <summary>
        /// Real convolution of <paramref name="signal"/> with <paramref name="taps"/>,
        /// returning a 'same'-length output centred on the filter (linear-phase delay removed).
        /// </summary>
        public static double[] Apply(double[] signal, double[] taps)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            if (taps == null) throw new ArgumentNullException(nameof(taps));
            int n = signal.Length;
            int m = taps.Length;
            var y = new double[n];
            if (m == 0 || n == 0) return y;

            int half = m / 2; // centre tap offset
            for (int i = 0; i < n; i++)
            {
                double acc = 0.0;
                // y[i] = sum_j taps[j] * signal[i + half - j]
                for (int j = 0; j < m; j++)
                {
                    int idx = i + half - j;
                    if (idx >= 0 && idx < n)
                        acc += taps[j] * signal[idx];
                }
                y[i] = acc;
            }
            return y;
        }

        /// <summary>
        /// Apply the real filter <paramref name="taps"/> independently to the I and Q rails,
        /// producing 'same'-length outputs.
        /// </summary>
        public static void ApplyComplex(double[] i, double[] q, double[] taps,
            out double[] oi, out double[] oq)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length)
                throw new ArgumentException("I and Q must have equal length.");
            oi = Apply(i, taps);
            oq = Apply(q, taps);
        }

        // ----- impulse responses -----

        private static double RcImpulse(double t, double beta)
        {
            double pit = Math.PI * t;
            double sinc;
            if (Math.Abs(t) < 1e-12)
                sinc = 1.0;
            else
                sinc = Math.Sin(pit) / pit;

            double denom = 1.0 - (2.0 * beta * t) * (2.0 * beta * t);
            double cosFactor;
            if (Math.Abs(denom) < 1e-9)
            {
                // Removable singularity at t = +/- 1/(2 beta): use the limit.
                cosFactor = Math.PI / 4.0 * Sinc(1.0 / (2.0 * beta));
                // Direct limit of RC: h = (pi/4) * sinc(1/(2 beta))
                return cosFactor;
            }
            return sinc * Math.Cos(Math.PI * beta * t) / denom;
        }

        private static double RrcImpulse(double t, double beta)
        {
            // Standard RRC closed form (taps in continuous time, t in symbol periods).
            if (Math.Abs(t) < 1e-12)
            {
                return 1.0 - beta + 4.0 * beta / Math.PI;
            }

            if (beta > 0.0)
            {
                double edge = 1.0 / (4.0 * beta);
                if (Math.Abs(Math.Abs(t) - edge) < 1e-9)
                {
                    double a = (1.0 + 2.0 / Math.PI) * Math.Sin(Math.PI / (4.0 * beta));
                    double b = (1.0 - 2.0 / Math.PI) * Math.Cos(Math.PI / (4.0 * beta));
                    return (beta / Math.Sqrt(2.0)) * (a + b);
                }
            }

            double pit = Math.PI * t;
            double num = Math.Sin(pit * (1.0 - beta))
                         + 4.0 * beta * t * Math.Cos(pit * (1.0 + beta));
            double den = pit * (1.0 - (4.0 * beta * t) * (4.0 * beta * t));
            return num / den;
        }

        private static double Sinc(double x)
        {
            if (Math.Abs(x) < 1e-12) return 1.0;
            double pix = Math.PI * x;
            return Math.Sin(pix) / pix;
        }

        // ----- helpers -----

        private static void NormalizeSum(double[] h)
        {
            double sum = 0.0;
            for (int k = 0; k < h.Length; k++) sum += h[k];
            if (Math.Abs(sum) < 1e-15) return; // avoid divide-by-zero; leave as-is
            double inv = 1.0 / sum;
            for (int k = 0; k < h.Length; k++) h[k] *= inv;
        }

        private static void ValidateArgs(double beta, int samplesPerSymbol, int spanSymbols)
        {
            if (beta < 0.0 || beta > 1.0)
                throw new ArgumentOutOfRangeException(nameof(beta), "Roll-off beta must be in [0, 1].");
            if (samplesPerSymbol < 1)
                throw new ArgumentOutOfRangeException(nameof(samplesPerSymbol));
            if (spanSymbols < 1)
                throw new ArgumentOutOfRangeException(nameof(spanSymbols));
        }
    }
}
