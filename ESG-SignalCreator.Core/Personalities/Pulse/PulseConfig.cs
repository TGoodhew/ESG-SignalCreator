using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Pulse
{
    /// <summary>
    /// Intra-pulse (on-pulse) modulation applied across the width of each pulse. These are the
    /// standard radar/EW pulse-compression waveforms. The first three members keep their original
    /// numeric values so projects saved by the v1 core still load; newer formats are appended.
    /// </summary>
    public enum IntraPulseModulation
    {
        /// <summary>Unmodulated (constant-phase) pulse — a simple gated CW burst.</summary>
        None = 0,

        /// <summary>Linear FM ("chirp"): instantaneous frequency sweeps linearly across the pulse.</summary>
        LinearFmChirp = 1,

        /// <summary>Binary phase code using a Barker sequence (0 or π phase per chip).</summary>
        BarkerPhase = 2,

        /// <summary>Non-linear FM chirp: a cubic frequency law (edge-weighted) for lower range sidelobes.</summary>
        NonLinearFmChirp = 3,

        /// <summary>Stepped-frequency: the band is swept in discrete constant-frequency steps.</summary>
        FmStep = 4,

        /// <summary>Stepped-amplitude: a rising amplitude staircase across the pulse (phase unmodulated).</summary>
        AmStep = 5,

        /// <summary>Binary phase-shift keying: a seeded pseudo-random 0/π phase code.</summary>
        Bpsk = 6,

        /// <summary>Quadrature phase-shift keying: a seeded pseudo-random 4-phase code.</summary>
        Qpsk = 7,

        /// <summary>Frank polyphase code (length N², N = <see cref="PulseConfig.FrankOrderN"/>).</summary>
        FrankCode = 8,

        /// <summary>P4 polyphase pulse-compression code (length <see cref="PulseConfig.PolyphaseLength"/>).</summary>
        PolyphaseP4 = 9
    }

    /// <summary>Pulse-repetition-interval patterning across the pulse train.</summary>
    public enum PriMode
    {
        /// <summary>A single fixed interval (<see cref="PulseConfig.PriSec"/>) between every pulse.</summary>
        Fixed = 0,

        /// <summary>A repeating (staggered) sequence of intervals from <see cref="PulseConfig.StaggerPatternSec"/>.</summary>
        Staggered = 1,

        /// <summary>The fixed interval plus a per-pulse uniform random jitter (± <see cref="PulseConfig.PriJitterSec"/>).</summary>
        Jittered = 2
    }

    /// <summary>
    /// Serializable settings for <see cref="PulsePersonality"/> — a pulse train with configurable
    /// timing, edge shaping, intra-pulse modulation, per-pulse frequency/phase/power offsets, and
    /// PRI patterning (fixed / staggered / jittered).
    /// </summary>
    /// <remarks>
    /// This covers the v1 core plus the synthesizable v2 additions (N7620A #179): the full
    /// intra-pulse modulation set, per-pulse offset tables (R-3), and staggered/jittered PRI (R-4).
    /// Antenna-scan patterning (R-5), pattern nesting (R-6), CSV import/export (R-7), and scenario
    /// impairments (R-9) — the N7620A Option 205/206 features — remain out of scope and tracked in #179.
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

        /// <summary>Nominal pulse repetition interval — time from one pulse start to the next, in seconds.
        /// Used directly in <see cref="Personalities.Pulse.PriMode.Fixed"/> and as the centre value in
        /// <see cref="Personalities.Pulse.PriMode.Jittered"/>.</summary>
        [DataMember] public double PriSec { get; set; } = 10e-6;

        /// <summary>Delay before the first pulse starts, in seconds.</summary>
        [DataMember] public double StartDelaySec { get; set; } = 0.0;

        /// <summary>Raised-cosine rise/fall time applied to each pulse edge, in seconds (0 = rectangular).</summary>
        [DataMember] public double RiseFallSec { get; set; } = 0.0;

        /// <summary>Intra-pulse (on-pulse) modulation applied across each pulse.</summary>
        [DataMember] public IntraPulseModulation Modulation { get; set; } = IntraPulseModulation.None;

        /// <summary>Total swept bandwidth for <see cref="IntraPulseModulation.LinearFmChirp"/>,
        /// <see cref="IntraPulseModulation.NonLinearFmChirp"/>, and <see cref="IntraPulseModulation.FmStep"/>,
        /// in hertz (frequency runs from -Bandwidth/2 to +Bandwidth/2 across the pulse).</summary>
        [DataMember] public double ChirpBandwidthHz { get; set; } = 5e6;

        /// <summary>Curvature for <see cref="IntraPulseModulation.NonLinearFmChirp"/>: 0 = linear,
        /// approaching 1 = increasingly edge-weighted (cubic) sweep. Clamped to [0, 1).</summary>
        [DataMember] public double NlfmCurvature { get; set; } = 0.5;

        /// <summary>Number of discrete steps for <see cref="IntraPulseModulation.FmStep"/> and
        /// <see cref="IntraPulseModulation.AmStep"/> (>= 1).</summary>
        [DataMember] public int IntraPulseStepCount { get; set; } = 8;

        /// <summary>Chip count for the <see cref="IntraPulseModulation.Bpsk"/> /
        /// <see cref="IntraPulseModulation.Qpsk"/> phase codes (>= 1).</summary>
        [DataMember] public int PhaseCodeChips { get; set; } = 13;

        /// <summary>Seed for the pseudo-random BPSK/QPSK phase code (repeatable).</summary>
        [DataMember] public int PhaseCodeSeed { get; set; } = 12345;

        /// <summary>Order N of the <see cref="IntraPulseModulation.FrankCode"/> (code length = N², N >= 1).</summary>
        [DataMember] public int FrankOrderN { get; set; } = 4;

        /// <summary>Length of the <see cref="IntraPulseModulation.PolyphaseP4"/> code (>= 1).</summary>
        [DataMember] public int PolyphaseLength { get; set; } = 16;

        /// <summary>Barker code length for <see cref="IntraPulseModulation.BarkerPhase"/>
        /// (valid: 2, 3, 4, 5, 7, 11, 13).</summary>
        [DataMember] public int BarkerLength { get; set; } = 13;

        /// <summary>Per-pulse carrier frequency offsets, in hertz, applied cyclically across the pulse
        /// train (pulse <c>j</c> uses element <c>j mod length</c>). Null/empty = no offset.</summary>
        [DataMember] public double[] PerPulseFrequencyOffsetsHz { get; set; }

        /// <summary>Per-pulse phase offsets, in degrees, applied cyclically across the pulse train.
        /// Null/empty = no offset.</summary>
        [DataMember] public double[] PerPulsePhaseOffsetsDeg { get; set; }

        /// <summary>Per-pulse power offsets, in dB, applied cyclically across the pulse train
        /// (relative levels; the composite is peak-normalized). Null/empty = no offset.</summary>
        [DataMember] public double[] PerPulsePowerOffsetsDb { get; set; }

        /// <summary>How the interval between successive pulses is chosen.</summary>
        [DataMember] public PriMode PriMode { get; set; } = PriMode.Fixed;

        /// <summary>Repeating sequence of intervals (seconds) used when
        /// <see cref="PriMode"/> is <see cref="Personalities.Pulse.PriMode.Staggered"/>. Each value
        /// must be >= <see cref="PulseWidthSec"/>. Null/empty falls back to <see cref="PriSec"/>.</summary>
        [DataMember] public double[] StaggerPatternSec { get; set; }

        /// <summary>Peak per-pulse PRI jitter (seconds) used when <see cref="PriMode"/> is
        /// <see cref="Personalities.Pulse.PriMode.Jittered"/>. The interval is
        /// <see cref="PriSec"/> ± a uniform random value in [-jitter, +jitter].</summary>
        [DataMember] public double PriJitterSec { get; set; } = 0.0;

        /// <summary>Seed for the PRI jitter generator (repeatable).</summary>
        [DataMember] public int PriJitterSeed { get; set; } = 1;

        /// <summary>When true, emit a one-sample marker (byte 1) at the start of every pulse.</summary>
        [DataMember] public bool EmitPulseMarkers { get; set; } = true;
    }
}
