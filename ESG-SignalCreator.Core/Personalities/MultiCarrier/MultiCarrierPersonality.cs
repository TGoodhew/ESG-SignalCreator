using System;
using System.Collections.Generic;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.MultiCarrier
{
    /// <summary>
    /// A multi-carrier signal personality: sums a set of independently configured complex-exponential
    /// carriers into a baseband I/Q waveform. Each enabled carrier contributes
    /// <c>amp * exp(j(2*pi*f*(n - delay)/fs + phase))</c>, where <c>amp = 10^(PowerDb/20)</c>, the
    /// phase is <see cref="Carrier.PhaseDeg"/> in radians, and <c>delay</c> is
    /// <see cref="Carrier.DelaySamples"/> applied circularly. The summed waveform is normalized so its
    /// peak vector magnitude is 1.0.
    /// </summary>
    public sealed class MultiCarrierPersonality : IWaveformPersonality
    {
        private MultiCarrierConfig _config = new MultiCarrierConfig();

        /// <inheritdoc/>
        public string Id => "multi-carrier";

        /// <inheritdoc/>
        public string DisplayName => "Multi-Carrier";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is MultiCarrierConfig mc))
                throw new ArgumentException("Expected a MultiCarrierConfig.", nameof(cfg));
            _config = mc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            MultiCarrierConfig cfg = _config ?? new MultiCarrierConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");

            // Collect enabled carriers in declaration order.
            var enabled = new List<Carrier>();
            if (cfg.Carriers != null)
            {
                foreach (Carrier c in cfg.Carriers)
                {
                    if (c != null && c.Enabled) enabled.Add(c);
                }
            }

            int n = cfg.Length;
            int count = enabled.Count;

            var amp = new double[count];
            var omega = new double[count];   // radians per sample
            var phase0 = new double[count];  // starting phase in radians
            var delay = new int[count];      // circular delay in samples, normalized to [0, n)
            for (int k = 0; k < count; k++)
            {
                Carrier c = enabled[k];
                amp[k] = Math.Pow(10.0, c.PowerDb / 20.0);
                omega[k] = 2.0 * Math.PI * c.FreqOffsetHz / cfg.SampleRateHz;
                phase0[k] = c.PhaseDeg * Math.PI / 180.0;
                // Wrap the delay into [0, n). C# '%' can be negative, so add n then re-mod.
                delay[k] = ((c.DelaySamples % n) + n) % n;
            }

            var i = new float[n];
            var q = new float[n];

            // Sum carriers sample by sample. With no enabled carriers the output is all zeros.
            // A delay shifts the carrier circularly: the value at output sample s is the carrier
            // evaluated at time index (s - delay), wrapped into [0, n). Because each carrier is a
            // pure complex exponential, evaluating at (s - delay) modulo n is equivalent to
            // evaluating the unwrapped phase at (s - delay) — the +/- n*omega wrap is absorbed into
            // the carrier's own periodicity only when omega*n is a multiple of 2*pi, so we compute
            // the wrapped time index explicitly to keep the shift exact for every offset.
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                double re = 0.0;
                double im = 0.0;
                for (int k = 0; k < count; k++)
                {
                    // Circular source index for this carrier.
                    int t = s - delay[k];
                    if (t < 0) t += n;
                    double arg = omega[k] * t + phase0[k];
                    re += amp[k] * Math.Cos(arg);
                    im += amp[k] * Math.Sin(arg);
                }
                i[s] = (float)re;
                q[s] = (float)im;

                if (progress != null && (s % reportEvery == 0))
                {
                    int pct = (int)((long)s * 100 / n);
                    if (pct != lastPct) { progress.Report(pct); lastPct = pct; }
                }
            }

            // Normalize so the peak vector magnitude is exactly 1.0.
            double peak = 0.0;
            for (int s = 0; s < n; s++)
            {
                double m = Math.Sqrt((double)i[s] * i[s] + (double)q[s] * q[s]);
                if (m > peak) peak = m;
            }
            if (peak > 0.0)
            {
                double scale = 1.0 / peak;
                for (int s = 0; s < n; s++)
                {
                    i[s] = (float)(i[s] * scale);
                    q[s] = (float)(q[s] * scale);
                }
            }

            progress?.Report(100);

            return new WaveformModel(i, q, cfg.SampleRateHz, "Multi-Carrier");
        }

        /// <summary>
        /// Build an array of <paramref name="count"/> evenly-spaced, enabled carriers centred on
        /// baseband centre, each with the given relative power. For an even count the carriers
        /// straddle the centre; for an odd count one sits on it. Phase and delay are zero.
        /// </summary>
        /// <param name="count">Number of carriers to generate (must be &gt;= 0).</param>
        /// <param name="spacingHz">Spacing between adjacent carriers, in hertz.</param>
        /// <param name="powerDbEach">Relative power assigned to every carrier, in dB.</param>
        public static Carrier[] EvenlySpaced(int count, double spacingHz, double powerDbEach)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var carriers = new Carrier[count];
            double mid = (count - 1) / 2.0;
            for (int k = 0; k < count; k++)
            {
                carriers[k] = new Carrier
                {
                    FreqOffsetHz = (k - mid) * spacingHz,
                    PowerDb = powerDbEach,
                    PhaseDeg = 0.0,
                    DelaySamples = 0,
                    Enabled = true
                };
            }
            return carriers;
        }
    }
}
