using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Jitter
{
    /// <summary>
    /// A jitter-injection personality (Signal Studio for Jitter Injection, E4438C-SP1): generates a
    /// sinusoidal clock/tone whose timing is modulated by periodic jitter (sinusoidal, square,
    /// triangle, saw-tooth, or exponential), Gaussian random jitter, or a composite of both — for
    /// receiver jitter-tolerance testing.
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

            int n = cfg.Length;
            double fs = cfg.SampleRateHz;
            double fclk = cfg.ClockRateHz;
            double uiSec = 1.0 / fclk;                       // one unit interval, seconds

            double periodicHalfSec = (cfg.PeriodicShape == JitterShape.None)
                ? 0.0
                : (cfg.PeriodicUiPp / 2.0) * uiSec;          // peak displacement, seconds
            double randomStdSec = cfg.RandomEnabled ? cfg.RandomUiRms * uiSec : 0.0;

            var i = new float[n];
            var q = new float[n];
            var rng = new GaussianRng(cfg.RandomSeed);

            double peakJit = 0.0;
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                double t = s / fs;

                double dt = 0.0;
                if (periodicHalfSec > 0.0)
                {
                    double phase01 = Frac(cfg.PeriodicRateHz * t);
                    dt += periodicHalfSec * PeriodicShapeValue(cfg.PeriodicShape, phase01);
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

        /// <summary>Fractional part in [0,1) (handles negative inputs).</summary>
        private static double Frac(double x)
        {
            double f = x - Math.Floor(x);
            return f < 0 ? f + 1.0 : f;
        }

        /// <summary>
        /// Value of the periodic jitter shape, normalized to [-1, +1], for a phase in [0,1).
        /// </summary>
        private static double PeriodicShapeValue(JitterShape shape, double p)
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
                case JitterShape.None:
                default:
                    return 0.0;
            }
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
