using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Cw
{
    /// <summary>
    /// CW / single-tone personality: a constant-envelope complex sinusoid
    /// I = A·cos(2π f n/fs + φ), Q = A·sin(2π f n/fs + φ).
    /// The sample count is chosen so the tone completes a whole number of cycles, giving a seamless
    /// loop with no wrap discontinuity. A zero offset degenerates to DC (I = A, Q = 0).
    /// </summary>
    public sealed class CwPersonality : IWaveformPersonality
    {
        /// <summary>Minimum ARB segment length on the E4438C.</summary>
        public const int MinLength = 60;

        private const double ZeroOffsetThresholdHz = 1e-9;

        private CwConfig _config = new CwConfig();

        public string Id => "cw";

        public string DisplayName => "CW / Single tone";

        /// <summary>Generic ARB; no special E4438C option required.</summary>
        public int? RequiredOption => null;

        public object GetConfig() => _config;

        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var c = cfg as CwConfig;
            if (c == null)
                throw new ArgumentException("Expected a CwConfig.", nameof(cfg));
            _config = c;
        }

        public WaveformModel Calculate(IProgress<int> progress)
        {
            CwConfig c = _config;
            double fs = c.SampleRateHz;
            if (fs <= 0) throw new ArgumentException("Sample rate must be positive.");

            double offset = c.FreqOffsetHz;
            double a = c.AmplitudeScale;
            double phi0 = c.PhaseDeg * Math.PI / 180.0;

            int n;
            double fActual;
            if (Math.Abs(offset) < ZeroOffsetThresholdHz)
            {
                n = ClampLength(c.Length);
                fActual = 0;
            }
            else
            {
                int cycles;
                n = SeamlessLength(c.Length, Math.Abs(offset), fs, out cycles);
                fActual = Math.Sign(offset) * cycles * fs / n;
            }

            var i = new float[n];
            var q = new float[n];
            double w = 2 * Math.PI * fActual / fs;

            progress?.Report(0);
            int nextReport = 0;
            for (int k = 0; k < n; k++)
            {
                double phase = w * k + phi0;
                i[k] = (float)(a * Math.Cos(phase));
                q[k] = (float)(a * Math.Sin(phase));

                if (progress != null && k >= nextReport)
                {
                    progress.Report((int)(100L * k / n));
                    nextReport = k + Math.Max(1, n / 100);
                }
            }
            progress?.Report(100);

            return new WaveformModel(i, q, fs, "CW");
        }

        /// <summary>
        /// Choose a sample count near <paramref name="target"/> holding a whole number of cycles of
        /// <paramref name="freq"/> at sample rate <paramref name="fs"/>, for seamless looping.
        /// </summary>
        private static int SeamlessLength(int target, double freq, double fs, out int cycles)
        {
            target = ClampLength(target);
            if (freq <= 0) { cycles = 0; return target; }

            cycles = (int)Math.Round(freq * target / fs);
            if (cycles < 1) cycles = 1;

            int n = (int)Math.Round(cycles * fs / freq);
            return ClampLength(n);
        }

        private static int ClampLength(int n)
        {
            return n < MinLength ? MinLength : n;
        }
    }
}
