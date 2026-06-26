using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Crest-factor reduction (CFR) by iterative clip-and-filter peak reduction.
    ///
    /// Each iteration:
    /// <list type="number">
    /// <item>Compute the RMS of the complex envelope, rms = sqrt(mean(I² + Q²)).</item>
    /// <item>Derive the clip threshold T = rms · 10^(<see cref="CfrConfig.TargetPaprDb"/> / 20).
    /// Any sample whose vector magnitude exceeds T is scaled radially back to T (phase
    /// preserved); samples below T are left untouched.</item>
    /// <item>If <see cref="CfrConfig.FilterAfterClip"/> is set, low-pass the clipped I/Q rails with
    /// a Hann-windowed-sinc FIR to suppress the spectral regrowth the hard clip introduces.</item>
    /// </list>
    /// The filter step re-grows some peaks, so the clip-then-filter pass is repeated
    /// <see cref="CfrConfig.Iterations"/> times to converge toward the target. The input is never
    /// mutated; a new <see cref="WaveformModel"/> with the same length, sample rate, and name is
    /// returned, renormalized so its peak vector magnitude is exactly 1.0.
    /// </summary>
    public static class Cfr
    {
        // Fixed FIR design: a 33-tap Hann-windowed-sinc low-pass. The cutoff is placed at half
        // of Nyquist (normalized cutoff fc = 0.25 of the sample rate) so the filter clearly
        // attenuates the broadband clipping products near the band edges while leaving the bulk
        // of a typical (oversampled) baseband signal intact. 33 taps gives a reasonably sharp
        // transition for a cheap linear-phase filter.
        private const int FilterTaps = 33;
        private const double NormalizedCutoff = 0.25; // cycles/sample (0.5 of Nyquist)

        /// <summary>
        /// Apply iterative clip-and-filter CFR to <paramref name="input"/> and return a new
        /// <see cref="WaveformModel"/>, renormalized to unit peak. The input is not modified.
        /// </summary>
        public static WaveformModel Apply(WaveformModel input, CfrConfig cfg)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (cfg.Iterations < 0)
                throw new ArgumentOutOfRangeException(nameof(cfg), "Iterations must be >= 0.");

            int n = input.Length;

            // Work in double precision on private copies; never touch the caller's arrays.
            var wi = new double[n];
            var wq = new double[n];
            for (int s = 0; s < n; s++)
            {
                wi[s] = input.I[s];
                wq[s] = input.Q[s];
            }

            double[] taps = cfg.FilterAfterClip ? DesignLowPass(FilterTaps, NormalizedCutoff) : null;

            for (int iter = 0; iter < cfg.Iterations; iter++)
            {
                // ---- RMS of the complex envelope: sqrt(mean(I^2 + Q^2)). ----
                double meanPower = 0.0;
                for (int s = 0; s < n; s++)
                    meanPower += wi[s] * wi[s] + wq[s] * wq[s];
                meanPower /= n;
                double rms = Math.Sqrt(meanPower);
                if (rms <= 0.0) break; // all-zero signal: nothing to clip

                // ---- Clip magnitude to threshold = rms * 10^(TargetPaprDb/20). ----
                double threshold = rms * Math.Pow(10.0, cfg.TargetPaprDb / 20.0);
                for (int s = 0; s < n; s++)
                {
                    double mag = Math.Sqrt(wi[s] * wi[s] + wq[s] * wq[s]);
                    if (mag > threshold)
                    {
                        double scale = threshold / mag; // radial clip, phase preserved
                        wi[s] *= scale;
                        wq[s] *= scale;
                    }
                }

                // ---- Low-pass to suppress clip-induced spectral regrowth. ----
                if (taps != null)
                {
                    Fir.ApplyComplex(wi, wq, taps, out double[] fi, out double[] fq);
                    wi = fi;
                    wq = fq;
                }
            }

            // ---- Renormalize to unit peak vector magnitude. ----
            double peak = 0.0;
            for (int s = 0; s < n; s++)
            {
                double m = wi[s] * wi[s] + wq[s] * wq[s];
                if (m > peak) peak = m;
            }
            peak = Math.Sqrt(peak);

            var oi = new float[n];
            var oq = new float[n];
            double norm = peak > 0.0 ? 1.0 / peak : 1.0;
            for (int s = 0; s < n; s++)
            {
                oi[s] = (float)(wi[s] * norm);
                oq[s] = (float)(wq[s] * norm);
            }

            return new WaveformModel(oi, oq, input.SampleRateHz, input.Name);
        }

        /// <summary>
        /// Convenience PAPR helper: the peak-to-average power ratio of a waveform, in dB.
        /// Delegates to <see cref="Ccdf.PaprDb(double[], double[])"/>.
        /// </summary>
        public static double PaprDb(WaveformModel wf)
        {
            if (wf == null) throw new ArgumentNullException(nameof(wf));
            int n = wf.Length;
            var i = new double[n];
            var q = new double[n];
            for (int s = 0; s < n; s++)
            {
                i[s] = wf.I[s];
                q[s] = wf.Q[s];
            }
            return Ccdf.PaprDb(i, q);
        }

        /// <summary>
        /// Design a Hann-windowed-sinc low-pass FIR of <paramref name="taps"/> taps (forced odd
        /// for a single centre tap / linear phase) with normalized cutoff <paramref name="fc"/>
        /// in cycles/sample. Taps are normalized to unit DC gain so the filter neither boosts nor
        /// attenuates the passband level.
        /// </summary>
        private static double[] DesignLowPass(int taps, double fc)
        {
            if (taps % 2 == 0) taps += 1; // ensure odd length, single centre tap
            var h = new double[taps];
            double[] win = Windowing.Hann(taps);
            int mid = taps / 2;

            for (int k = 0; k < taps; k++)
            {
                int m = k - mid;
                double sinc = (m == 0) ? 2.0 * fc : Math.Sin(2.0 * Math.PI * fc * m) / (Math.PI * m);
                h[k] = sinc * win[k];
            }

            // Normalize to unit DC gain (sum of taps == 1).
            double sum = 0.0;
            for (int k = 0; k < taps; k++) sum += h[k];
            if (Math.Abs(sum) > 1e-15)
            {
                double inv = 1.0 / sum;
                for (int k = 0; k < taps; k++) h[k] *= inv;
            }
            return h;
        }
    }
}
