using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Jitter
{
    /// <summary>
    /// Periodic-jitter waveform shape. These modulate the timing (phase) of the underlying clock/tone.
    /// Custom/user-defined profiles and standards masks (e.g. ITU-T G.8251) are deferred follow-ups.
    /// </summary>
    public enum JitterShape
    {
        /// <summary>No periodic jitter.</summary>
        None,
        /// <summary>Sinusoidal jitter (the classic SJ tone used for jitter-tolerance masks).</summary>
        Sinusoidal,
        /// <summary>Square-wave jitter (±peak displacement).</summary>
        Square,
        /// <summary>Triangle-wave jitter.</summary>
        Triangle,
        /// <summary>Saw-tooth (ramp) jitter.</summary>
        SawTooth,
        /// <summary>Exponential-ramp jitter.</summary>
        Exponential
    }

    /// <summary>
    /// Serializable settings for <see cref="JitterPersonality"/> — a jittered clock/tone stimulus for
    /// receiver jitter-tolerance testing (Signal Studio for Jitter Injection, E4438C-SP1). Jitter is
    /// applied as timing (phase) modulation of a sinusoidal clock; periodic and random jitter can be
    /// combined (composite). Amplitudes are expressed in unit intervals (UI), 1 UI = one clock period.
    /// </summary>
    /// <remarks>
    /// v1 covers periodic (sinusoidal/square/triangle/saw-tooth/exponential) + random (Gaussian) +
    /// composite jitter with digital repeatability. Deferred: custom periodic profiles, predefined
    /// standards masks (ITU-T G.8251 OC-48/192/768), and automated SJ frequency sweeps.
    /// </remarks>
    [DataContract]
    public sealed class JitterConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 100e6;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember] public int Length { get; set; } = 65536;

        /// <summary>Underlying clock/tone rate being jittered (baseband offset frequency), in hertz.</summary>
        [DataMember] public double ClockRateHz { get; set; } = 10e6;

        /// <summary>Periodic-jitter waveform shape.</summary>
        [DataMember] public JitterShape PeriodicShape { get; set; } = JitterShape.Sinusoidal;

        /// <summary>Periodic-jitter rate (jitter frequency), in hertz.</summary>
        [DataMember] public double PeriodicRateHz { get; set; } = 100e3;

        /// <summary>Periodic-jitter amplitude, peak-to-peak, in unit intervals (UI).</summary>
        [DataMember] public double PeriodicUiPp { get; set; } = 0.2;

        /// <summary>When true, add Gaussian random jitter.</summary>
        [DataMember] public bool RandomEnabled { get; set; } = false;

        /// <summary>Random-jitter standard deviation (RMS), in unit intervals (UI).</summary>
        [DataMember] public double RandomUiRms { get; set; } = 0.01;

        /// <summary>Seed for the random-jitter generator so sequences are digitally repeatable.</summary>
        [DataMember] public int RandomSeed { get; set; } = 12345;
    }
}
