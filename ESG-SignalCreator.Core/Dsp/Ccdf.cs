using System;

namespace EsgSignalCreator.Dsp
{
    /// <summary>
    /// Peak-to-average power ratio (PAPR), crest factor, and complementary cumulative
    /// distribution function (CCDF) for complex I/Q signals.
    ///
    /// Instantaneous power is p[n] = I[n]^2 + Q[n]^2. PAPR is the ratio of the peak power
    /// to the mean power, expressed in dB.
    /// </summary>
    public static class Ccdf
    {
        /// <summary>
        /// Peak-to-average power ratio in dB: 10*log10(peakPower / meanPower).
        /// A constant-envelope signal returns ~0 dB.
        /// </summary>
        public static double PaprDb(double[] i, double[] q)
        {
            MeanAndPeakPower(i, q, out double mean, out double peak);
            if (mean <= 0.0) return 0.0;
            return 10.0 * Math.Log10(peak / mean);
        }

        /// <summary>
        /// Crest factor (linear, voltage ratio): peakAmplitude / rmsAmplitude.
        /// Equals 10^(PaprDb/20).
        /// </summary>
        public static double CrestFactor(double[] i, double[] q)
        {
            MeanAndPeakPower(i, q, out double mean, out double peak);
            if (mean <= 0.0) return 1.0;
            return Math.Sqrt(peak / mean);
        }

        /// <summary>
        /// CCDF curve: for a set of dB-above-average levels, the probability that the
        /// instantaneous power exceeds the mean power by at least that many dB.
        /// </summary>
        /// <param name="dbAboveAvg">Output x-axis: dB above average power (0..maxDb).</param>
        /// <param name="probability">
        /// Output y-axis: probability P(power/mean >= 10^(x/10)) at each level.
        /// </param>
        /// <param name="maxDb">Highest dB-above-average level to evaluate.</param>
        /// <param name="points">Number of points along the curve (>= 2).</param>
        public static void Curve(double[] i, double[] q,
            out double[] dbAboveAvg, out double[] probability,
            double maxDb = 12.0, int points = 121)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length)
                throw new ArgumentException("I and Q must have equal length.");
            if (points < 2) throw new ArgumentOutOfRangeException(nameof(points));
            if (maxDb <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxDb));

            int n = i.Length;
            dbAboveAvg = new double[points];
            probability = new double[points];

            double mean = 0.0;
            for (int k = 0; k < n; k++)
                mean += i[k] * i[k] + q[k] * q[k];
            if (n > 0) mean /= n;

            double step = maxDb / (points - 1);
            for (int p = 0; p < points; p++)
                dbAboveAvg[p] = p * step;

            if (n == 0 || mean <= 0.0)
                return; // all probabilities stay 0

            // Threshold[p] in linear power = mean * 10^(db/10).
            for (int p = 0; p < points; p++)
            {
                double threshold = mean * Math.Pow(10.0, dbAboveAvg[p] / 10.0);
                int count = 0;
                for (int k = 0; k < n; k++)
                {
                    double pw = i[k] * i[k] + q[k] * q[k];
                    if (pw >= threshold) count++;
                }
                probability[p] = (double)count / n;
            }
        }

        private static void MeanAndPeakPower(double[] i, double[] q, out double mean, out double peak)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length)
                throw new ArgumentException("I and Q must have equal length.");

            int n = i.Length;
            mean = 0.0;
            peak = 0.0;
            for (int k = 0; k < n; k++)
            {
                double pw = i[k] * i[k] + q[k] * q[k];
                if (pw > peak) peak = pw;
                mean += pw;
            }
            if (n > 0) mean /= n;
        }
    }
}
