using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Inline FIR filter / correction impairment. Designs a windowed-sinc (Hann-windowed)
    /// linear-phase FIR for the response requested by a <see cref="FilterConfig"/> at the input
    /// waveform's own sample rate, then applies it to both I and Q rails via
    /// <see cref="Fir.ApplyComplex"/>.
    ///
    /// <para>Design conventions (all frequencies baseband, fc = CutoffHz / SampleRateHz cycles/sample):</para>
    /// <list type="bullet">
    /// <item><b>Low-pass</b>: ideal sinc lowpass at the cutoff, Hann-windowed, then normalized to
    /// unit DC gain (taps sum to 1).</item>
    /// <item><b>High-pass</b>: spectral inversion of the unit-DC low-pass — negate every tap and
    /// add 1 to the centre tap (an all-pass minus the low-pass). DC gain ≈ 0, Nyquist gain ≈ 1.</item>
    /// <item><b>Band-pass</b>: difference of two unit-DC low-passes at <c>CutoffHz + BandwidthHz/2</c>
    /// and <c>CutoffHz − BandwidthHz/2</c>.</item>
    /// </list>
    /// The input is never mutated; a new <see cref="WaveformModel"/> with the same length, sample
    /// rate, and name is returned.
    /// </summary>
    public static class FilterImpairment
    {
        /// <summary>
        /// Design and apply the filter described by <paramref name="cfg"/> to
        /// <paramref name="input"/>, returning a new <see cref="WaveformModel"/>. Not in place.
        /// </summary>
        public static WaveformModel Apply(WaveformModel input, FilterConfig cfg)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            double[] taps = DesignTaps(cfg, input.SampleRateHz);

            int n = input.Length;
            var di = new double[n];
            var dq = new double[n];
            for (int s = 0; s < n; s++)
            {
                di[s] = input.I[s];
                dq[s] = input.Q[s];
            }

            Fir.ApplyComplex(di, dq, taps, out double[] oi, out double[] oq);

            var fi = new float[n];
            var fq = new float[n];
            for (int s = 0; s < n; s++)
            {
                fi[s] = (float)oi[s];
                fq[s] = (float)oq[s];
            }

            return new WaveformModel(fi, fq, input.SampleRateHz, input.Name);
        }

        /// <summary>
        /// Design the FIR tap array for <paramref name="cfg"/> at <paramref name="sampleRateHz"/>.
        /// Exposed for testing. The returned length is <c>cfg.Taps</c> forced odd.
        /// </summary>
        public static double[] DesignTaps(FilterConfig cfg, double sampleRateHz)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (sampleRateHz <= 0.0)
                throw new ArgumentException("Sample rate must be positive.", nameof(sampleRateHz));
            if (cfg.Taps < 1)
                throw new ArgumentOutOfRangeException(nameof(cfg), "Taps must be >= 1.");

            // Force an odd tap count for a single centre tap and exact symmetry (linear phase).
            int taps = cfg.Taps;
            if ((taps & 1) == 0) taps += 1;

            switch (cfg.Type)
            {
                case FilterType.LowPass:
                    return LowPass(cfg.CutoffHz / sampleRateHz, taps);

                case FilterType.HighPass:
                    return HighPass(cfg.CutoffHz / sampleRateHz, taps);

                case FilterType.BandPass:
                {
                    double fc = cfg.CutoffHz / sampleRateHz;
                    double halfBw = (cfg.BandwidthHz / sampleRateHz) / 2.0;
                    double fHigh = fc + halfBw;
                    double fLow = fc - halfBw;
                    if (fLow < 0.0) fLow = 0.0;

                    double[] hHigh = LowPass(fHigh, taps);
                    double[] hLow = LowPass(fLow, taps);
                    var h = new double[taps];
                    for (int k = 0; k < taps; k++)
                        h[k] = hHigh[k] - hLow[k]; // band = LP(high edge) - LP(low edge)
                    return h;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(cfg), "Unknown filter type.");
            }
        }

        /// <summary>
        /// Hann-windowed ideal low-pass, normalized to unit DC gain.
        /// </summary>
        /// <param name="fcNorm">Cutoff in cycles/sample (0..0.5). Clamped to that range.</param>
        private static double[] LowPass(double fcNorm, int taps)
        {
            if (fcNorm < 0.0) fcNorm = 0.0;
            if (fcNorm > 0.5) fcNorm = 0.5;

            double[] win = Windowing.Hann(taps);
            var h = new double[taps];
            int mid = taps / 2; // centre index (taps is odd)

            // Ideal LP impulse response: h_ideal[m] = 2*fc * sinc(2*fc*m), m = k - mid.
            double twoFc = 2.0 * fcNorm;
            for (int k = 0; k < taps; k++)
            {
                int m = k - mid;
                double sinc = Sinc(twoFc * m);
                h[k] = twoFc * sinc * win[k];
            }

            NormalizeSum(h);
            return h;
        }

        /// <summary>
        /// High-pass via spectral inversion of a unit-DC low-pass: −h_lp with +1 added at centre.
        /// </summary>
        private static double[] HighPass(double fcNorm, int taps)
        {
            double[] h = LowPass(fcNorm, taps); // unit DC gain
            int mid = taps / 2;
            for (int k = 0; k < taps; k++) h[k] = -h[k];
            h[mid] += 1.0; // all-pass (delta) minus low-pass
            return h;
        }

        // Normalized sinc: sinc(x) = sin(pi*x)/(pi*x), sinc(0) = 1.
        private static double Sinc(double x)
        {
            if (Math.Abs(x) < 1e-12) return 1.0;
            double pix = Math.PI * x;
            return Math.Sin(pix) / pix;
        }

        private static void NormalizeSum(double[] h)
        {
            double sum = 0.0;
            for (int k = 0; k < h.Length; k++) sum += h[k];
            if (Math.Abs(sum) < 1e-15) return; // avoid divide-by-zero; leave as-is
            double inv = 1.0 / sum;
            for (int k = 0; k < h.Length; k++) h[k] *= inv;
        }
    }
}
