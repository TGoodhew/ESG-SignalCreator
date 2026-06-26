using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Inline AWGN impairment: adds deterministic complex white Gaussian noise to an existing
    /// waveform at a target carrier-to-noise ratio.
    ///
    /// Pipeline:
    /// <list type="number">
    /// <item>Measure the input signal's average power P_s = mean(I² + Q²).</item>
    /// <item>Compute the target noise power P_n = P_s / 10^(C/N_dB / 10), split evenly across the
    /// I and Q rails (each rail gets variance P_n / 2).</item>
    /// <item>Generate independent N(0, P_n/2) samples per rail via the Box–Muller transform driven
    /// by a <see cref="Random"/> seeded with <see cref="AwgnImpairmentConfig.RandomSeed"/>, and add
    /// them to the signal.</item>
    /// <item>If <see cref="AwgnImpairmentConfig.RenormalizePeak"/> is set, scale the sum so its peak
    /// vector magnitude is exactly 1.0.</item>
    /// </list>
    /// The input is never mutated; a new <see cref="WaveformModel"/> with the same length, sample
    /// rate, and name is returned.
    /// </summary>
    public static class AwgnImpairment
    {
        /// <summary>
        /// Add noise to <paramref name="input"/> at the C/N specified by <paramref name="cfg"/>
        /// and return a new <see cref="WaveformModel"/>. The input is not modified.
        /// </summary>
        public static WaveformModel Apply(WaveformModel input, AwgnImpairmentConfig cfg)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            int n = input.Length;
            float[] si = input.I;
            float[] sq = input.Q;

            // ---- 1. Average signal power P_s = mean(I^2 + Q^2). ----
            double signalPower = 0.0;
            for (int s = 0; s < n; s++)
                signalPower += (double)si[s] * si[s] + (double)sq[s] * sq[s];
            signalPower /= n;

            // ---- 2. Target noise power P_n, split across I and Q (variance P_n/2 each). ----
            double noisePower = signalPower / Math.Pow(10.0, cfg.CarrierToNoiseDb / 10.0);
            double rsd = Math.Sqrt(noisePower / 2.0); // per-rail standard deviation

            // ---- 3. Add deterministic complex Gaussian noise (Box–Muller). ----
            var oi = new float[n];
            var oq = new float[n];
            var rng = new Random(cfg.RandomSeed);
            for (int s = 0; s < n; s++)
            {
                double u1 = rng.NextDouble();
                double u2 = rng.NextDouble();
                if (u1 < 1e-300) u1 = 1e-300; // guard against log(0); NextDouble() is in [0,1)
                double r = Math.Sqrt(-2.0 * Math.Log(u1));
                double theta = 2.0 * Math.PI * u2;
                double ni = rsd * r * Math.Cos(theta);
                double nq = rsd * r * Math.Sin(theta);

                oi[s] = (float)(si[s] + ni);
                oq[s] = (float)(sq[s] + nq);
            }

            // ---- 4. Optionally renormalize to unit peak vector magnitude. ----
            if (cfg.RenormalizePeak)
            {
                double peak = 0.0;
                for (int s = 0; s < n; s++)
                {
                    double m = (double)oi[s] * oi[s] + (double)oq[s] * oq[s];
                    if (m > peak) peak = m;
                }
                peak = Math.Sqrt(peak);
                if (peak > 0.0)
                {
                    double norm = 1.0 / peak;
                    for (int s = 0; s < n; s++)
                    {
                        oi[s] = (float)(oi[s] * norm);
                        oq[s] = (float)(oq[s] * norm);
                    }
                }
            }

            return new WaveformModel(oi, oq, input.SampleRateHz, input.Name);
        }

        /// <summary>
        /// Estimate the carrier-to-noise ratio, in dB, between an <paramref name="original"/>
        /// signal and a <paramref name="noisy"/> version of it, by treating the per-sample
        /// difference (noisy − scaled original) as the residual noise.
        /// </summary>
        /// <remarks>
        /// Because <see cref="Apply"/> may renormalize the output, the noisy waveform can be a
        /// scaled copy of (signal + noise). This helper first estimates a least-squares scale
        /// factor <c>k</c> that best maps the original onto the noisy waveform, then measures the
        /// residual power of <c>noisy − k·original</c> as the noise, and <c>k·original</c> as the
        /// signal. The returned value is 10·log10(signalPower / noisePower).
        /// </remarks>
        public static double MeasuredCnDb(WaveformModel original, WaveformModel noisy)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (noisy == null) throw new ArgumentNullException(nameof(noisy));
            if (original.Length != noisy.Length)
                throw new ArgumentException("Waveforms must have equal length.");

            int n = original.Length;
            float[] xi = original.I, xq = original.Q;
            float[] yi = noisy.I, yq = noisy.Q;

            // Least-squares scale k minimizing |y - k*x|^2 over complex samples:
            // k = Re(<y, x>) / <x, x>  (the imaginary part is ignored since a real scalar gain
            // is assumed; renormalization in Apply is a real positive scale).
            double dot = 0.0;   // sum(yi*xi + yq*xq)
            double xx = 0.0;    // sum(xi^2 + xq^2)
            for (int s = 0; s < n; s++)
            {
                dot += (double)yi[s] * xi[s] + (double)yq[s] * xq[s];
                xx += (double)xi[s] * xi[s] + (double)xq[s] * xq[s];
            }
            double k = xx > 0.0 ? dot / xx : 0.0;

            double signalPower = 0.0; // power of k*x
            double noisePower = 0.0;  // power of residual y - k*x
            for (int s = 0; s < n; s++)
            {
                double sgI = k * xi[s];
                double sgQ = k * xq[s];
                signalPower += sgI * sgI + sgQ * sgQ;

                double rI = yi[s] - sgI;
                double rQ = yq[s] - sgQ;
                noisePower += rI * rI + rQ * rQ;
            }
            signalPower /= n;
            noisePower /= n;

            if (noisePower <= 0.0) return double.PositiveInfinity;
            if (signalPower <= 0.0) return double.NegativeInfinity;
            return 10.0 * Math.Log10(signalPower / noisePower);
        }
    }
}
