using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Seamless
{
    /// <summary>
    /// Static analyzer/fixer for ARB loop seams.
    ///
    /// An arbitrary waveform played in continuous (looping) mode wraps from its last sample
    /// s[N-1] back to its first sample s[0]. If those two samples differ appreciably in level
    /// or phase, the wrap produces a periodic glitch (a "seam") at the loop rate, spraying
    /// spectral spurs. A clean waveform completes an integer number of cycles so that the
    /// step from s[N-1] to s[0] is small.
    ///
    /// See issue #50 / SignalCreation-UX-Requirements §5.1, §9.
    /// </summary>
    public static class SeamlessGuard
    {
        /// <summary>
        /// Magnitude of the wrap step |s[0] - s[N-1]| (the jump the player takes when the
        /// loop wraps), normalized by the signal RMS so the result is dimensionless and
        /// scale-invariant. Larger = worse seam. A perfectly continuous loop returns ~0.
        /// </summary>
        public static double WrapDiscontinuity(WaveformModel wf)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));

            int n = wf.Length;
            float[] i = wf.I;
            float[] q = wf.Q;

            double di = (double)i[0] - i[n - 1];
            double dq = (double)q[0] - q[n - 1];
            double step = Math.Sqrt(di * di + dq * dq);

            double rms = Rms(i, q);
            if (rms <= 0.0)
            {
                // Degenerate all-zero (or near-zero) signal: the seam is whatever the raw step is.
                return step;
            }
            return step / rms;
        }

        /// <summary>
        /// Phase jump (radians) from the last sample s[N-1] to the first sample s[0],
        /// i.e. atan2(q0,i0) - atan2(qLast,iLast), wrapped to (-pi, +pi].
        /// </summary>
        public static double PhaseStepRadians(WaveformModel wf)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));

            int n = wf.Length;
            double phase0 = Math.Atan2(wf.Q[0], wf.I[0]);
            double phaseLast = Math.Atan2(wf.Q[n - 1], wf.I[n - 1]);
            return WrapToPi(phase0 - phaseLast);
        }

        /// <summary>
        /// True when the wrap discontinuity is within <paramref name="tolerance"/> (default 1e-3),
        /// i.e. the waveform loops seamlessly.
        /// </summary>
        public static bool IsSeamless(WaveformModel wf, double tolerance = 1e-3)
        {
            return WrapDiscontinuity(wf) <= tolerance;
        }

        /// <summary>
        /// Return a trimmed copy whose end aligns more closely with its start, by dropping
        /// trailing samples back to the nearest point where I crosses zero with the same sign
        /// trend (rising/falling) that I exhibits at the start of the waveform. This nudges the
        /// loop toward an integer-cycle length.
        ///
        /// Best-effort: never trims below <paramref name="minLength"/> samples, and if no
        /// suitable crossing is found the input is returned unchanged. The input is never mutated.
        /// </summary>
        public static WaveformModel TrimToZeroCrossing(WaveformModel wf, int minLength = 60)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));
            if (minLength < 1) minLength = 1;

            int n = wf.Length;
            if (n <= minLength) return wf;

            float[] i = wf.I;

            // Determine the sign trend of I at the very start of the waveform: the direction in
            // which I is moving as it leaves s[0]. We want the trim point to leave I crossing
            // zero heading the same way, so that s[0] follows on smoothly after the wrap.
            int startTrend = Sign((double)i[1] - i[0]);
            if (startTrend == 0)
            {
                // I is momentarily flat at the start; fall back to the slope a little later.
                for (int k = 2; k < n && startTrend == 0; k++)
                    startTrend = Sign((double)i[k] - i[k - 1]);
                if (startTrend == 0) return wf; // I is constant — no meaningful crossing.
            }

            // Scan backward from the end, looking for the last index newLast (>= minLength-1)
            // where the segment [0 .. newLast] would wrap with I crossing zero in the same
            // trend. A wrap crossing occurs between sample newLast and sample 0: we look for
            // i[newLast] and i[0] straddling zero (or i[newLast] == 0) with the matching trend.
            for (int newLast = n - 1; newLast >= minLength - 1; newLast--)
            {
                double cur = i[newLast];
                double next = i[0]; // the sample that follows after the wrap

                // Trend of the wrap step from i[newLast] -> i[0].
                int wrapTrend = Sign(next - cur);
                if (wrapTrend != startTrend) continue;

                // Zero crossing on the wrap edge: cur and next have opposite signs (straddle 0),
                // or cur sits essentially on zero.
                bool straddles = (cur <= 0.0 && next >= 0.0) || (cur >= 0.0 && next <= 0.0);
                if (!straddles) continue;

                int newLen = newLast + 1;
                if (newLen == n) return wf; // already aligned; nothing to trim.

                return Trim(wf, newLen);
            }

            return wf; // no good crossing — leave unchanged.
        }

        private static WaveformModel Trim(WaveformModel wf, int newLen)
        {
            float[] i = new float[newLen];
            float[] q = new float[newLen];
            Array.Copy(wf.I, i, newLen);
            Array.Copy(wf.Q, q, newLen);

            byte[] markers = null;
            if (wf.Markers != null)
            {
                markers = new byte[newLen];
                Array.Copy(wf.Markers, markers, newLen);
            }

            return new WaveformModel(i, q, wf.SampleRateHz, wf.Name, markers);
        }

        private static double Rms(float[] i, float[] q)
        {
            int n = i.Length;
            double sum = 0.0;
            for (int k = 0; k < n; k++)
            {
                sum += (double)i[k] * i[k] + (double)q[k] * q[k];
            }
            return Math.Sqrt(sum / n);
        }

        private static double WrapToPi(double angle)
        {
            const double twoPi = 2.0 * Math.PI;
            while (angle > Math.PI) angle -= twoPi;
            while (angle <= -Math.PI) angle += twoPi;
            return angle;
        }

        private static int Sign(double x)
        {
            if (x > 0.0) return 1;
            if (x < 0.0) return -1;
            return 0;
        }
    }
}
