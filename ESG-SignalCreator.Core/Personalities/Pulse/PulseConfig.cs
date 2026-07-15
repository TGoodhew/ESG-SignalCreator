using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Pulse
{
    /// <summary>
    /// Intra-pulse (on-pulse) modulation applied across the width of each pulse.
    /// These are the core radar/EW pulse-compression waveforms; advanced formats
    /// (non-linear FM, Frank/polyphase codes) are deferred (see the N7620A requirements doc).
    /// </summary>
    public enum IntraPulseModulation
    {
        /// <summary>Unmodulated (constant-phase) pulse — a simple gated CW burst.</summary>
        None,

        /// <summary>Linear FM ("chirp"): instantaneous frequency sweeps linearly across the pulse.</summary>
        LinearFmChirp,

        /// <summary>Binary phase code using a Barker sequence (0 or π phase per chip).</summary>
        BarkerPhase
    }

    /// <summary>
    /// Serializable settings for <see cref="PulsePersonality"/> — a uniform pulse train with
    /// configurable timing, edge shaping, and optional intra-pulse modulation.
    /// </summary>
    /// <remarks>
    /// This is the v1 core of Signal Studio for Pulse Building (N7620A). It builds a single
    /// pulse shape and repeats it at a fixed pulse-repetition interval to fill the waveform.
    /// Per-pulse offset tables, staggered/jittered PRI, antenna-scan patterning, and CSV import
    /// (N7620A Options 205/206) are intentionally out of scope here and tracked as follow-ups.
    /// </remarks>
    [DataContract]
    public sealed class PulseConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 50e6;

        /// <summary>Total number of complex samples to generate. Pulses tile across this length.</summary>
        [DataMember] public int Length { get; set; } = 65536;

        /// <summary>Pulse width (the "on" time of each pulse), in seconds.</summary>
        [DataMember] public double PulseWidthSec { get; set; } = 1e-6;

        /// <summary>Pulse repetition interval — time from one pulse start to the next, in seconds.</summary>
        [DataMember] public double PriSec { get; set; } = 10e-6;

        /// <summary>Delay before the first pulse starts, in seconds.</summary>
        [DataMember] public double StartDelaySec { get; set; } = 0.0;

        /// <summary>Raised-cosine rise/fall time applied to each pulse edge, in seconds (0 = rectangular).</summary>
        [DataMember] public double RiseFallSec { get; set; } = 0.0;

        /// <summary>Intra-pulse (on-pulse) modulation applied across each pulse.</summary>
        [DataMember] public IntraPulseModulation Modulation { get; set; } = IntraPulseModulation.None;

        /// <summary>Total swept bandwidth for <see cref="IntraPulseModulation.LinearFmChirp"/>, in hertz
        /// (frequency runs from -Bandwidth/2 to +Bandwidth/2 across the pulse).</summary>
        [DataMember] public double ChirpBandwidthHz { get; set; } = 5e6;

        /// <summary>Barker code length for <see cref="IntraPulseModulation.BarkerPhase"/>
        /// (valid: 2, 3, 4, 5, 7, 11, 13).</summary>
        [DataMember] public int BarkerLength { get; set; } = 13;

        /// <summary>When true, emit a one-sample marker (byte 1) at the start of every pulse.</summary>
        [DataMember] public bool EmitPulseMarkers { get; set; } = true;
    }
}
