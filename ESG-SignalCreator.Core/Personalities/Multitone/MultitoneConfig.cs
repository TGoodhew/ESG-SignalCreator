using System;
using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Multitone
{
    /// <summary>
    /// How the per-tone starting phases are chosen when summing a multitone signal.
    /// The choice trades off crest factor (PAPR) versus simplicity / reproducibility.
    /// </summary>
    public enum PhaseStrategy
    {
        /// <summary>Deterministic pseudo-random phases drawn from a seeded generator.</summary>
        Random,

        /// <summary>All tones start at zero phase (worst case crest factor).</summary>
        Equal,

        /// <summary>Newman/Schroeder quadratic phasing: phase = pi * k^2 / N for the k-th of N tones.</summary>
        Newman
    }

    /// <summary>
    /// A single tone in the multitone comb: an offset from baseband centre, a relative
    /// power, an explicit phase (only honoured when <see cref="PhaseStrategy"/> permits),
    /// and an enable flag so tones can be toggled without removing them.
    /// </summary>
    [DataContract]
    public sealed class Tone
    {
        /// <summary>Frequency offset from baseband centre, in hertz (may be negative).</summary>
        [DataMember] public double FreqOffsetHz { get; set; }

        /// <summary>Relative tone power in dB; linear amplitude is 10^(PowerDb/20).</summary>
        [DataMember] public double PowerDb { get; set; }

        /// <summary>Explicit starting phase in degrees (used only for informational/round-trip purposes).</summary>
        [DataMember] public double PhaseDeg { get; set; }

        /// <summary>When false, the tone is skipped during calculation.</summary>
        [DataMember] public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Serializable settings for <see cref="MultitonePersonality"/>.
    /// </summary>
    [DataContract]
    public sealed class MultitoneConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 10e6;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember] public int Length { get; set; } = 16384;

        /// <summary>Per-tone phase assignment strategy.</summary>
        [DataMember] public PhaseStrategy Phase { get; set; } = PhaseStrategy.Newman;

        /// <summary>Seed for <see cref="PhaseStrategy.Random"/> so results are reproducible.</summary>
        [DataMember] public int RandomSeed { get; set; } = 12345;

        /// <summary>The tones making up the comb. Disabled tones are ignored.</summary>
        [DataMember] public Tone[] Tones { get; set; } = Array.Empty<Tone>();
    }
}
