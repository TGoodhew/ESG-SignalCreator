using System;
using System.Collections.Generic;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Multitone
{
    /// <summary>
    /// A multitone signal personality: sums a comb of complex-exponential tones into a
    /// baseband I/Q waveform. Each enabled tone contributes
    /// <c>amp * exp(j(2*pi*f*t + phase))</c>, where <c>amp = 10^(PowerDb/20)</c> and the
    /// phase comes from the configured <see cref="PhaseStrategy"/>. The summed waveform is
    /// normalized so its peak vector magnitude is 1.0.
    /// </summary>
    public sealed class MultitonePersonality : IWaveformPersonality
    {
        private MultitoneConfig _config = new MultitoneConfig();

        /// <inheritdoc/>
        public string Id => "multitone";

        /// <inheritdoc/>
        public string DisplayName => "Multitone";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <summary>The peak-to-average-power ratio (dB) of the most recent <see cref="Calculate"/> result.</summary>
        public double LastPaprDb { get; private set; }

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is MultitoneConfig mc))
                throw new ArgumentException("Expected a MultitoneConfig.", nameof(cfg));
            _config = mc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            MultitoneConfig cfg = _config ?? new MultitoneConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");

            // Collect enabled tones in declaration order; the index within this list is the
            // 'k' used by the phase strategies.
            var enabled = new List<Tone>();
            if (cfg.Tones != null)
            {
                foreach (Tone t in cfg.Tones)
                {
                    if (t != null && t.Enabled) enabled.Add(t);
                }
            }

            int n = cfg.Length;
            int count = enabled.Count;

            double[] phase0 = ComputeStartPhases(cfg, enabled, count);
            double[] amp = new double[count];
            double[] omega = new double[count]; // radians per sample
            for (int k = 0; k < count; k++)
            {
                amp[k] = Math.Pow(10.0, enabled[k].PowerDb / 20.0);
                omega[k] = 2.0 * Math.PI * enabled[k].FreqOffsetHz / cfg.SampleRateHz;
            }

            var i = new float[n];
            var q = new float[n];

            // Sum tones sample by sample. With no enabled tones the output is all zeros.
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                double re = 0.0;
                double im = 0.0;
                for (int k = 0; k < count; k++)
                {
                    double arg = omega[k] * s + phase0[k];
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

            LastPaprDb = PaprDb(i, q);
            progress?.Report(100);

            return new WaveformModel(i, q, cfg.SampleRateHz, "Multitone");
        }

        /// <summary>
        /// Compute the starting phase (radians) for each of <paramref name="count"/> enabled tones
        /// according to the configured strategy.
        /// </summary>
        private static double[] ComputeStartPhases(MultitoneConfig cfg, List<Tone> enabled, int count)
        {
            var phases = new double[count];
            switch (cfg.Phase)
            {
                case PhaseStrategy.Equal:
                    // already all zero
                    break;

                case PhaseStrategy.Manual:
                    for (int k = 0; k < count; k++)
                        phases[k] = enabled[k].PhaseDeg * Math.PI / 180.0;
                    break;

                case PhaseStrategy.Random:
                    var rng = new Random(cfg.RandomSeed);
                    for (int k = 0; k < count; k++)
                        phases[k] = rng.NextDouble() * 2.0 * Math.PI;
                    break;

                case PhaseStrategy.Newman:
                default:
                    // Newman / Schroeder quadratic phasing: phi_k = pi * k^2 / N.
                    int nTones = count > 0 ? count : 1;
                    for (int k = 0; k < count; k++)
                        phases[k] = Math.PI * ((double)k * k) / nTones;
                    break;
            }
            return phases;
        }

        /// <summary>
        /// Build an array of equally-spaced, enabled tones centred on <paramref name="centerOffsetHz"/>.
        /// For an even count the tones straddle the centre; for an odd count one sits on it.
        /// </summary>
        /// <param name="toneCount">Number of tones to generate (must be &gt;= 0).</param>
        /// <param name="spacingHz">Spacing between adjacent tones, in hertz.</param>
        /// <param name="centerOffsetHz">Frequency offset of the comb centre from baseband centre.</param>
        /// <param name="powerDbPerTone">Relative power assigned to every tone, in dB.</param>
        public static Tone[] AutoSpacing(int toneCount, double spacingHz, double centerOffsetHz, double powerDbPerTone)
        {
            if (toneCount < 0) throw new ArgumentOutOfRangeException(nameof(toneCount));

            var tones = new Tone[toneCount];
            // Symmetric placement: offsets run from -(N-1)/2 .. +(N-1)/2 in steps of one.
            double mid = (toneCount - 1) / 2.0;
            for (int k = 0; k < toneCount; k++)
            {
                tones[k] = new Tone
                {
                    FreqOffsetHz = centerOffsetHz + (k - mid) * spacingHz,
                    PowerDb = powerDbPerTone,
                    PhaseDeg = 0.0,
                    Enabled = true
                };
            }
            return tones;
        }

        /// <summary>
        /// Peak-to-average-power ratio, in dB, of the complex envelope: 20*log10(peak/rms),
        /// where peak and rms are taken over the instantaneous magnitude sqrt(I^2 + Q^2).
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

            // 10*log10(peakPower/meanPower) == 20*log10(peakMag/rmsMag).
            return 10.0 * Math.Log10(peakPower / meanPower);
        }
    }
}
