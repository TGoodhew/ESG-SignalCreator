using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Multitone;

namespace EsgSignalCreator.Personalities.MultitoneDistortion
{
    /// <summary>
    /// A multitone-distortion / NPR stimulus personality (Signal Studio for Multitone Distortion,
    /// N7621B): builds a dense comb of equally-spaced tones for intermodulation-distortion (IMD)
    /// testing, with an optional cleared notch for noise-power-ratio (NPR) testing. The composite
    /// crest factor is controlled by a per-tone phase preset (random / parabolic / constant).
    /// </summary>
    /// <remarks>
    /// The tone summation, unit-peak normalization, and PAPR calculation are delegated to the tested
    /// <see cref="MultitonePersonality"/>; this personality's job is to lay out the comb, apply the
    /// notch, and map the phase preset. Spectrum-analyzer-assisted pre-distortion (N7621B R-7) is a
    /// deferred follow-up.
    /// </remarks>
    public sealed class MultitoneDistortionPersonality : IWaveformPersonality
    {
        private MultitoneDistortionConfig _config = new MultitoneDistortionConfig();

        /// <inheritdoc/>
        public string Id => "multitone-distortion";

        /// <inheritdoc/>
        public string DisplayName => "Multitone Distortion";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <summary>Peak-to-average-power ratio (dB) of the most recent <see cref="Calculate"/> result.</summary>
        public double LastPaprDb { get; private set; }

        /// <summary>Number of tones actually emitted (comb tones minus any removed by the notch).</summary>
        public int LastActiveToneCount { get; private set; }

        /// <summary>Composite comb bandwidth (ToneCount × spacing), in hertz, of the last configuration.</summary>
        public double LastNoiseBandwidthHz { get; private set; }

        /// <summary>Whether the most recent <see cref="Calculate"/> used explicit per-tone phases
        /// (a per-tone phase table or pre-distortion phase correction) rather than the phase preset.</summary>
        public bool LastUsedManualPhase { get; private set; }

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is MultitoneDistortionConfig mc))
                throw new ArgumentException("Expected a MultitoneDistortionConfig.", nameof(cfg));
            _config = mc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            MultitoneDistortionConfig cfg = _config ?? new MultitoneDistortionConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");
            if (cfg.ToneCount < 2)
                throw new InvalidOperationException("ToneCount must be at least 2.");
            if (cfg.ToneCount > 4097)
                throw new InvalidOperationException("ToneCount must not exceed 4097.");
            if (cfg.ToneSpacingHz <= 0)
                throw new InvalidOperationException("ToneSpacingHz must be positive.");
            if (cfg.NotchEnabled && cfg.NotchWidthHz < 0)
                throw new InvalidOperationException("NotchWidthHz must be >= 0.");

            // Lay out the equally-spaced comb, then optionally clear a notch band.
            Tone[] tones = MultitonePersonality.AutoSpacing(
                cfg.ToneCount, cfg.ToneSpacingHz, cfg.CenterOffsetHz, cfg.PowerDbPerTone);

            // Apply the per-tone magnitude/phase tables and (if enabled) pre-distortion correction.
            ComputePerTone(cfg, tones.Length, out double[] magDb, out double[] phaseDeg, out bool manualPhase);
            for (int k = 0; k < tones.Length; k++)
            {
                tones[k].PowerDb = magDb[k];
                if (manualPhase) tones[k].PhaseDeg = phaseDeg[k];
            }
            LastUsedManualPhase = manualPhase;

            int active = tones.Length;
            if (cfg.NotchEnabled && cfg.NotchWidthHz > 0)
            {
                double notchCenter = cfg.CenterOffsetHz + cfg.NotchOffsetHz;
                double half = cfg.NotchWidthHz / 2.0;
                double lo = notchCenter - half;
                double hi = notchCenter + half;
                active = 0;
                foreach (Tone t in tones)
                {
                    bool inNotch = t.FreqOffsetHz >= lo && t.FreqOffsetHz <= hi;
                    t.Enabled = !inNotch;
                    if (t.Enabled) active++;
                }
            }

            var inner = new MultitonePersonality();
            inner.LoadConfig(new MultitoneConfig
            {
                SampleRateHz = cfg.SampleRateHz,
                Length = cfg.Length,
                Phase = manualPhase ? PhaseStrategy.Manual : MapPhase(cfg.Phase),
                RandomSeed = cfg.RandomSeed,
                Tones = tones
            });

            WaveformModel wf = inner.Calculate(progress);
            wf.Name = "Multitone Distortion";

            LastPaprDb = inner.LastPaprDb;
            LastActiveToneCount = active;
            LastNoiseBandwidthHz = cfg.ToneCount * cfg.ToneSpacingHz;

            return wf;
        }

        /// <summary>
        /// Compute the effective per-tone magnitude (dB) and phase (deg) for a <paramref name="toneCount"/>-tone
        /// comb, combining the per-tone tables (or the uniform power / phase preset) with pre-distortion
        /// correction. This is the correction math, decoupled from waveform synthesis so it can be tested.
        /// </summary>
        /// <param name="magnitudeDb">Effective per-tone magnitude, in dB (length = <paramref name="toneCount"/>).</param>
        /// <param name="phaseDeg">Effective per-tone phase, in degrees (meaningful only when <paramref name="manualPhase"/> is true).</param>
        /// <param name="manualPhase">True when explicit per-tone phases are in play (a per-tone phase table
        /// and/or pre-distortion phase correction) and the phase preset should be bypassed.</param>
        public static void ComputePerTone(MultitoneDistortionConfig cfg, int toneCount,
            out double[] magnitudeDb, out double[] phaseDeg, out bool manualPhase)
        {
            bool hasMagTable = cfg.PerToneMagnitudeDb != null && cfg.PerToneMagnitudeDb.Length > 0;
            bool hasPhaseTable = cfg.PerTonePhaseDeg != null && cfg.PerTonePhaseDeg.Length > 0;
            bool predistort = cfg.PredistortionEnabled;
            bool hasMagErr = predistort && cfg.MeasuredToneMagnitudeErrorDb != null && cfg.MeasuredToneMagnitudeErrorDb.Length > 0;
            bool hasPhaseErr = predistort && cfg.MeasuredTonePhaseErrorDeg != null && cfg.MeasuredTonePhaseErrorDeg.Length > 0;

            manualPhase = hasPhaseTable || hasPhaseErr;
            magnitudeDb = new double[toneCount];
            phaseDeg = new double[toneCount];

            for (int k = 0; k < toneCount; k++)
            {
                double mag = hasMagTable ? Cycle(cfg.PerToneMagnitudeDb, k) : cfg.PowerDbPerTone;
                if (hasMagErr) mag -= Cycle(cfg.MeasuredToneMagnitudeErrorDb, k); // invert the measured error
                magnitudeDb[k] = mag;

                if (manualPhase)
                {
                    double ph = hasPhaseTable ? Cycle(cfg.PerTonePhaseDeg, k) : 0.0;
                    if (hasPhaseErr) ph -= Cycle(cfg.MeasuredTonePhaseErrorDeg, k);
                    phaseDeg[k] = ph;
                }
            }
        }

        /// <summary>Cyclically index a table (returns 0 for a null/empty table).</summary>
        private static double Cycle(double[] table, int index)
            => (table != null && table.Length > 0) ? table[index % table.Length] : 0.0;

        /// <summary>Map the N7621B-style phase preset onto the multitone engine's phase strategy.</summary>
        private static PhaseStrategy MapPhase(MultitonePhasePreset preset)
        {
            switch (preset)
            {
                case MultitonePhasePreset.Random: return PhaseStrategy.Random;
                case MultitonePhasePreset.Constant: return PhaseStrategy.Equal;
                case MultitonePhasePreset.Parabolic:
                default: return PhaseStrategy.Newman;
            }
        }
    }
}
