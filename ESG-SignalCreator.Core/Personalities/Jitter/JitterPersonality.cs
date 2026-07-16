using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Jitter
{
    /// <summary>
    /// A jitter-injection personality (Signal Studio for Jitter Injection, E4438C-SP1): generates a
    /// sinusoidal clock/tone whose timing is modulated by periodic jitter (sinusoidal, square,
    /// triangle, saw-tooth, exponential, or a user-defined custom profile), Gaussian random jitter, or
    /// a composite — plus a sinusoidal-jitter frequency sweep (optionally following a tolerance mask)
    /// for building jitter-tolerance masks — for receiver jitter-tolerance testing.
    /// </summary>
    /// <remarks>
    /// Jitter is a timing displacement Δt(t) applied to the clock, so the output is
    /// <c>exp(j·2π·fclk·(t + Δt(t)))</c>. Because it is pure phase/timing modulation, the complex
    /// envelope magnitude stays 1.0 everywhere. Amplitudes are expressed in unit intervals (UI), where
    /// 1 UI = one clock period = 1/<see cref="JitterConfig.ClockRateHz"/> seconds. Random jitter is a
    /// seeded per-sample Gaussian process, so identical settings reproduce an identical sequence.
    /// </remarks>
    public sealed class JitterPersonality : IWaveformPersonality
    {
        private JitterConfig _config = new JitterConfig();

        /// <inheritdoc/>
        public string Id => "jitter";

        /// <inheritdoc/>
        public string DisplayName => "Jitter Injection";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <summary>Peak absolute timing deviation (seconds) applied in the most recent result.</summary>
        public double LastPeakJitterSec { get; private set; }

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is JitterConfig jc))
                throw new ArgumentException("Expected a JitterConfig.", nameof(cfg));
            _config = jc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            JitterConfig cfg = _config ?? new JitterConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");
            if (cfg.ClockRateHz <= 0)
                throw new InvalidOperationException("ClockRateHz must be positive.");
            if (cfg.PeriodicUiPp < 0)
                throw new InvalidOperationException("PeriodicUiPp must be >= 0.");
            if (cfg.RandomEnabled && cfg.RandomUiRms < 0)
                throw new InvalidOperationException("RandomUiRms must be >= 0.");
            EnforceRange(cfg);

            int n = cfg.Length;
            double fs = cfg.SampleRateHz;
            double fclk = cfg.ClockRateHz;
            double uiSec = 1.0 / fclk;                       // one unit interval, seconds

            bool sweeping = cfg.SweepEnabled && cfg.PeriodicShape != JitterShape.None;
            double periodicHalfSec = (cfg.PeriodicShape == JitterShape.None)
                ? 0.0
                : (cfg.PeriodicUiPp / 2.0) * uiSec;          // peak displacement, seconds
            double randomStdSec = cfg.RandomEnabled ? cfg.RandomUiRms * uiSec : 0.0;

            double[] customShape = cfg.PeriodicShape == JitterShape.Custom ? cfg.CustomShapeSamples : null;

            var i = new float[n];
            var q = new float[n];
            var rng = new GaussianRng(cfg.RandomSeed);

            double peakJit = 0.0;
            double sweepTheta = 0.0;                          // accumulated SJ phase for the sweep
            double recordSec = n / fs;
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                double t = s / fs;

                double dt = 0.0;
                if (sweeping)
                {
                    // Swept sinusoidal jitter: instantaneous SJ frequency ramps across the record;
                    // amplitude is either constant or read from the tolerance mask at that frequency.
                    double fj = SweepFrequency(cfg, recordSec > 0 ? t / recordSec : 0.0);
                    double uiPp = cfg.SweepFollowMask
                        ? JitterMasks.AmplitudeUiPp(cfg.MaskStandard, fj, cfg.CustomMaskFreqHz, cfg.CustomMaskUiPp)
                        : cfg.PeriodicUiPp;
                    sweepTheta += 2.0 * Math.PI * fj / fs;
                    dt += (uiPp / 2.0) * uiSec * Math.Sin(sweepTheta);
                }
                else if (periodicHalfSec > 0.0)
                {
                    double phase01 = Frac(cfg.PeriodicRateHz * t);
                    dt += periodicHalfSec * PeriodicShapeValue(cfg.PeriodicShape, phase01, customShape);
                }
                if (randomStdSec > 0.0)
                    dt += randomStdSec * rng.NextStandard();

                double a = Math.Abs(dt);
                if (a > peakJit) peakJit = a;

                double phase = 2.0 * Math.PI * fclk * (t + dt);
                i[s] = (float)Math.Cos(phase);
                q[s] = (float)Math.Sin(phase);

                if (progress != null && (s % reportEvery == 0))
                {
                    int pct = (int)((long)s * 100 / n);
                    if (pct != lastPct) { progress.Report(pct); lastPct = pct; }
                }
            }

            LastPeakJitterSec = peakJit;
            progress?.Report(100);

            return new WaveformModel(i, q, fs, "Jitter Injection");
        }

        /// <summary>
        /// Enforce the achievable jitter range (E4438C-SP1 R-10): the clock and every jitter frequency
        /// must sit below Nyquist, and the periodic amplitude must not exceed an optional hardware cap.
        /// </summary>
        private static void EnforceRange(JitterConfig cfg)
        {
            double nyquist = cfg.SampleRateHz / 2.0;
            if (cfg.ClockRateHz >= nyquist)
                throw new InvalidOperationException("ClockRateHz must be below Nyquist (SampleRateHz/2).");

            if (cfg.SweepEnabled && cfg.PeriodicShape != JitterShape.None)
            {
                if (cfg.SweepStartHz <= 0 || cfg.SweepStopHz <= 0)
                    throw new InvalidOperationException("Sweep frequencies must be positive.");
                if (Math.Max(cfg.SweepStartHz, cfg.SweepStopHz) >= nyquist)
                    throw new InvalidOperationException("Sweep frequencies must be below Nyquist (SampleRateHz/2).");
            }
            else if (cfg.PeriodicShape != JitterShape.None && cfg.PeriodicRateHz >= nyquist)
            {
                throw new InvalidOperationException("PeriodicRateHz must be below Nyquist (SampleRateHz/2).");
            }

            if (cfg.MaxJitterUiPp > 0 && cfg.PeriodicUiPp > cfg.MaxJitterUiPp)
                throw new InvalidOperationException(
                    "PeriodicUiPp (" + cfg.PeriodicUiPp + ") exceeds the configured MaxJitterUiPp (" + cfg.MaxJitterUiPp + ").");
        }

        /// <summary>The instantaneous SJ sweep frequency (Hz) at normalized record position u in [0,1].</summary>
        private static double SweepFrequency(JitterConfig cfg, double u)
        {
            if (u < 0) u = 0; else if (u > 1) u = 1;
            double f0 = cfg.SweepStartHz, f1 = cfg.SweepStopHz;
            if (cfg.SweepMode == JitterSweepMode.Logarithmic && f0 > 0 && f1 > 0)
                return Math.Exp(Math.Log(f0) + u * (Math.Log(f1) - Math.Log(f0)));
            return f0 + u * (f1 - f0);
        }

        /// <summary>Fractional part in [0,1) (handles negative inputs).</summary>
        private static double Frac(double x)
        {
            double f = x - Math.Floor(x);
            return f < 0 ? f + 1.0 : f;
        }

        /// <summary>
        /// Value of the periodic jitter shape, normalized to [-1, +1], for a phase in [0,1). The
        /// <paramref name="customShape"/> table (one period) is used for <see cref="JitterShape.Custom"/>.
        /// </summary>
        private static double PeriodicShapeValue(JitterShape shape, double p, double[] customShape)
        {
            switch (shape)
            {
                case JitterShape.Sinusoidal:
                    return Math.Sin(2.0 * Math.PI * p);
                case JitterShape.Square:
                    return p < 0.5 ? 1.0 : -1.0;
                case JitterShape.Triangle:
                    // 0 -> +1 at 1/4, 0 at 1/2, -1 at 3/4, 0 at 1: use the standard triangle.
                    return p < 0.5 ? (1.0 - 4.0 * Math.Abs(p - 0.25)) : (-1.0 + 4.0 * Math.Abs(p - 0.75));
                case JitterShape.SawTooth:
                    return 2.0 * p - 1.0;                      // ramp -1 -> +1
                case JitterShape.Exponential:
                    // Exponential ramp from -1 to +1 across the period.
                    const double k = 3.0;
                    return 2.0 * (Math.Exp(k * p) - 1.0) / (Math.Exp(k) - 1.0) - 1.0;
                case JitterShape.Custom:
                    return CustomShapeValue(customShape, p);
                case JitterShape.None:
                default:
                    return 0.0;
            }
        }

        /// <summary>Linearly interpolate a one-period custom shape table (cyclic) at phase p in [0,1).</summary>
        private static double CustomShapeValue(double[] shape, double p)
        {
            if (shape == null || shape.Length == 0) return 0.0;
            if (shape.Length == 1) return shape[0];
            double x = p * shape.Length;              // sample position within the period
            int k0 = (int)Math.Floor(x) % shape.Length;
            if (k0 < 0) k0 += shape.Length;
            int k1 = (k0 + 1) % shape.Length;
            double frac = x - Math.Floor(x);
            return shape[k0] + frac * (shape[k1] - shape[k0]);
        }

        /// <summary>Seeded standard-normal generator (Box–Muller) for repeatable random jitter.</summary>
        private sealed class GaussianRng
        {
            private readonly Random _rng;
            private double _spare;
            private bool _hasSpare;

            public GaussianRng(int seed) { _rng = new Random(seed); }

            public double NextStandard()
            {
                if (_hasSpare) { _hasSpare = false; return _spare; }
                double u1, u2;
                do { u1 = _rng.NextDouble(); } while (u1 <= 1e-12);
                u2 = _rng.NextDouble();
                double mag = Math.Sqrt(-2.0 * Math.Log(u1));
                _spare = mag * Math.Sin(2.0 * Math.PI * u2);
                _hasSpare = true;
                return mag * Math.Cos(2.0 * Math.PI * u2);
            }
        }
    }
}
