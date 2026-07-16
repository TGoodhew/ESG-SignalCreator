using System;

namespace EsgSignalCreator.Personalities.Jitter
{
    /// <summary>
    /// Sinusoidal-jitter tolerance masks (SJ amplitude, in UI peak-to-peak, versus jitter frequency)
    /// used by the SJ frequency sweep to trace a receiver's required jitter tolerance.
    /// </summary>
    /// <remarks>
    /// The predefined ITU-T G.8251 (OTN) masks below use the standard mask <em>shape</em> — a
    /// low-frequency amplitude plateau, a −20 dB/decade roll-off, and the OTN 0.15 UI<sub>pp</sub>
    /// high-frequency floor — with corner frequencies that scale with the line rate. <b>The corner
    /// values are approximate/representative and MUST be verified against ITU-T G.8251 before being
    /// used for conformance.</b> Amplitude between breakpoints is interpolated log-log (giving the
    /// straight −20 dB/decade segments); outside the breakpoints the nearest endpoint amplitude holds.
    /// </remarks>
    public static class JitterMasks
    {
        // Each mask: ascending breakpoint frequencies (Hz) paired with amplitudes (UI peak-to-peak).
        // Shape: flat plateau -> -20 dB/decade roll-off (a factor-100 drop over 2 decades) -> 0.15 UI floor.
        private static readonly double[] Oc48Freq = { 100, 6_500, 650_000, 5_000_000 };
        private static readonly double[] Oc48Ui = { 15.0, 15.0, 0.15, 0.15 };

        private static readonly double[] Oc192Freq = { 100, 26_000, 2_600_000, 20_000_000 };
        private static readonly double[] Oc192Ui = { 15.0, 15.0, 0.15, 0.15 };

        private static readonly double[] Oc768Freq = { 100, 100_000, 10_000_000, 80_000_000 };
        private static readonly double[] Oc768Ui = { 15.0, 15.0, 0.15, 0.15 };

        /// <summary>
        /// The SJ amplitude (UI peak-to-peak) the given <paramref name="mask"/> requires at frequency
        /// <paramref name="freqHz"/>. Custom masks use <paramref name="customFreq"/>/<paramref name="customUi"/>.
        /// Returns 0 for <see cref="JitterMask.None"/> or an empty custom mask.
        /// </summary>
        public static double AmplitudeUiPp(JitterMask mask, double freqHz, double[] customFreq, double[] customUi)
        {
            switch (mask)
            {
                case JitterMask.G8251Oc48: return Interp(Oc48Freq, Oc48Ui, freqHz);
                case JitterMask.G8251Oc192: return Interp(Oc192Freq, Oc192Ui, freqHz);
                case JitterMask.G8251Oc768: return Interp(Oc768Freq, Oc768Ui, freqHz);
                case JitterMask.Custom:
                    if (customFreq == null || customUi == null || customFreq.Length == 0 ||
                        customFreq.Length != customUi.Length)
                        return 0.0;
                    return Interp(customFreq, customUi, freqHz);
                case JitterMask.None:
                default:
                    return 0.0;
            }
        }

        /// <summary>Log-log interpolation of an ascending (freq, amplitude) breakpoint table.</summary>
        private static double Interp(double[] f, double[] a, double x)
        {
            if (f.Length == 1 || x <= f[0]) return a[0];
            if (x >= f[f.Length - 1]) return a[f.Length - 1];

            int k = 1;
            while (k < f.Length && x > f[k]) k++;
            double f0 = f[k - 1], f1 = f[k], a0 = a[k - 1], a1 = a[k];

            // Interpolate linearly in log-log space so constant-slope (dB/decade) segments are exact.
            if (f0 <= 0 || f1 <= 0 || a0 <= 0 || a1 <= 0)
            {
                double tLin = (x - f0) / (f1 - f0);
                return a0 + tLin * (a1 - a0);
            }
            double t = (Math.Log(x) - Math.Log(f0)) / (Math.Log(f1) - Math.Log(f0));
            return Math.Exp(Math.Log(a0) + t * (Math.Log(a1) - Math.Log(a0)));
        }
    }
}
