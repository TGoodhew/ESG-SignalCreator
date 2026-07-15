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
                Phase = MapPhase(cfg.Phase),
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
