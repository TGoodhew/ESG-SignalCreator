using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Awgn
{
    /// <summary>
    /// Band-limited additive white Gaussian noise (AWGN) personality.
    ///
    /// Generation pipeline:
    /// <list type="number">
    /// <item>Complex white Gaussian noise: independent N(0,1) samples on the I and Q rails,
    /// produced by the Box–Muller transform driven by a <see cref="Random"/> seeded with
    /// <see cref="AwgnConfig.RandomSeed"/> (so output is fully deterministic for a given seed).</item>
    /// <item>Optional band-limiting: a Hann-windowed-sinc FIR low-pass with cutoff at half the
    /// requested two-sided <see cref="AwgnConfig.NoiseBandwidthHz"/>, applied to each rail via
    /// <see cref="Fir.ApplyComplex"/>. If the requested bandwidth is &gt;= the sample rate the
    /// noise is left full-band (no filter).</item>
    /// <item>Crest-factor clipping: the complex envelope magnitude is clipped at
    /// <see cref="AwgnConfig.CrestFactorDb"/> above its RMS. Non-positive values disable clipping.</item>
    /// <item>Normalization: the peak vector magnitude is scaled to exactly 1.0.</item>
    /// </list>
    /// </summary>
    public sealed class AwgnPersonality : IWaveformPersonality
    {
        /// <summary>Half-span of the windowed-sinc low-pass, in samples (taps = 2*HalfTaps + 1).</summary>
        private const int FilterHalfTaps = 64;

        private AwgnConfig _config = new AwgnConfig();

        /// <inheritdoc/>
        public string Id => "awgn";

        /// <inheritdoc/>
        public string DisplayName => "AWGN";

        /// <summary>Generic ARB; no special E4438C option required.</summary>
        public int? RequiredOption => null;

        /// <summary>RMS (dB) of the complex envelope of the most recent <see cref="Calculate"/> result.</summary>
        public double LastRmsDb { get; private set; }

        /// <summary>PAPR (dB) of the most recent <see cref="Calculate"/> result.</summary>
        public double LastPaprDb { get; private set; }

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is AwgnConfig ac))
                throw new ArgumentException("Expected an AwgnConfig.", nameof(cfg));
            _config = ac;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            AwgnConfig cfg = _config ?? new AwgnConfig();

            double fs = cfg.SampleRateHz;
            if (fs <= 0) throw new InvalidOperationException("SampleRateHz must be positive.");
            int n = cfg.Length;
            if (n <= 0) throw new InvalidOperationException("Length must be positive.");

            progress?.Report(0);

            // ---- 1. Complex white Gaussian noise via Box–Muller (deterministic). ----
            var di = new double[n];
            var dq = new double[n];
            var rng = new Random(cfg.RandomSeed);
            for (int s = 0; s < n; s++)
            {
                // Two independent unit-variance Gaussians per iteration -> I and Q rails.
                double u1 = rng.NextDouble();
                double u2 = rng.NextDouble();
                // Guard against log(0); NextDouble() is in [0,1).
                if (u1 < 1e-300) u1 = 1e-300;
                double r = Math.Sqrt(-2.0 * Math.Log(u1));
                double theta = 2.0 * Math.PI * u2;
                di[s] = r * Math.Cos(theta);
                dq[s] = r * Math.Sin(theta);
            }
            progress?.Report(30);

            // ---- 2. Optional band-limiting low-pass (Hann-windowed sinc). ----
            // NoiseBandwidthHz is the two-sided occupied bandwidth, so the low-pass cutoff is
            // half of it. A bandwidth >= fs means "full band": skip filtering.
            if (cfg.NoiseBandwidthHz > 0 && cfg.NoiseBandwidthHz < fs)
            {
                double cutoffHz = cfg.NoiseBandwidthHz / 2.0;
                double[] taps = DesignLowPass(cutoffHz, fs, FilterHalfTaps);
                Fir.ApplyComplex(di, dq, taps, out di, out dq);
            }
            progress?.Report(55);

            // ---- 3. Crest-factor clipping of the complex envelope. ----
            if (cfg.CrestFactorDb > 0)
            {
                double rms = ComplexRms(di, dq);
                if (rms > 0)
                {
                    // Clip magnitude at CrestFactorDb above RMS: limit = rms * 10^(CF/20).
                    double limit = rms * Math.Pow(10.0, cfg.CrestFactorDb / 20.0);
                    for (int s = 0; s < n; s++)
                    {
                        double mag = Math.Sqrt(di[s] * di[s] + dq[s] * dq[s]);
                        if (mag > limit)
                        {
                            double scale = limit / mag;
                            di[s] *= scale;
                            dq[s] *= scale;
                        }
                    }
                }
            }
            progress?.Report(75);

            // ---- 4. Normalize peak vector magnitude to 1.0. ----
            double peak = 0.0;
            for (int s = 0; s < n; s++)
            {
                double m = di[s] * di[s] + dq[s] * dq[s];
                if (m > peak) peak = m;
            }
            peak = Math.Sqrt(peak);

            var i = new float[n];
            var q = new float[n];
            double norm = peak > 0.0 ? 1.0 / peak : 1.0;
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                i[s] = (float)(di[s] * norm);
                q[s] = (float)(dq[s] * norm);

                if (progress != null && (s % reportEvery == 0))
                {
                    int pct = 75 + (int)((long)s * 25 / n);
                    if (pct != lastPct) { progress.Report(pct); lastPct = pct; }
                }
            }

            LastRmsDb = RmsDb(i, q);
            LastPaprDb = PaprDb(i, q);
            progress?.Report(100);

            return new WaveformModel(i, q, fs, "AWGN");
        }

        /// <summary>
        /// Design a real, linear-phase low-pass FIR by windowing an ideal sinc with a Hann window.
        /// The filter has <c>2*halfTaps + 1</c> taps and unit DC gain.
        /// </summary>
        /// <param name="cutoffHz">One-sided cutoff frequency, in hertz.</param>
        /// <param name="fs">Sample rate, in hertz.</param>
        /// <param name="halfTaps">Half the span; the filter has 2*halfTaps+1 taps.</param>
        private static double[] DesignLowPass(double cutoffHz, double fs, int halfTaps)
        {
            int taps = 2 * halfTaps + 1;
            var h = new double[taps];
            double[] win = Windowing.Hann(taps);

            // Normalized cutoff in cycles/sample (0..0.5). 2*fc/fs is the sinc scale.
            double fc = cutoffHz / fs; // cycles per sample
            for (int k = 0; k < taps; k++)
            {
                int m = k - halfTaps;
                double sinc;
                if (m == 0)
                    sinc = 2.0 * fc;
                else
                    sinc = Math.Sin(2.0 * Math.PI * fc * m) / (Math.PI * m);
                h[k] = sinc * win[k];
            }

            // Normalize to unit DC gain so filtering preserves overall level.
            double sum = 0.0;
            for (int k = 0; k < taps; k++) sum += h[k];
            if (Math.Abs(sum) > 1e-15)
            {
                double inv = 1.0 / sum;
                for (int k = 0; k < taps; k++) h[k] *= inv;
            }
            return h;
        }

        private static double ComplexRms(double[] i, double[] q)
        {
            int n = i.Length;
            if (n == 0) return 0.0;
            double sum = 0.0;
            for (int s = 0; s < n; s++)
                sum += i[s] * i[s] + q[s] * q[s];
            return Math.Sqrt(sum / n);
        }

        /// <summary>
        /// RMS of the complex envelope, in dB relative to unit magnitude: 20*log10(rms),
        /// where rms = sqrt(mean(I^2 + Q^2)). Returns <see cref="double.NegativeInfinity"/>
        /// for an empty or all-zero waveform.
        /// </summary>
        public static double RmsDb(float[] i, float[] q)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length == 0) return double.NegativeInfinity;

            double sum = 0.0;
            for (int s = 0; s < i.Length; s++)
                sum += (double)i[s] * i[s] + (double)q[s] * q[s];
            double meanPower = sum / i.Length;
            if (meanPower <= 0.0) return double.NegativeInfinity;
            return 10.0 * Math.Log10(meanPower);
        }

        /// <summary>
        /// Peak-to-average-power ratio, in dB, of the complex envelope: 10*log10(peakPower/meanPower).
        /// Returns 0 for an empty or all-zero waveform.
        /// </summary>
        public static double PaprDb(float[] i, float[] q)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length == 0) return 0.0;

            double peakPower = 0.0;
            double sumPower = 0.0;
            for (int s = 0; s < i.Length; s++)
            {
                double p = (double)i[s] * i[s] + (double)q[s] * q[s];
                if (p > peakPower) peakPower = p;
                sumPower += p;
            }
            double meanPower = sumPower / i.Length;
            if (meanPower <= 0.0 || peakPower <= 0.0) return 0.0;
            return 10.0 * Math.Log10(peakPower / meanPower);
        }
    }
}
