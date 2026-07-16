using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Jitter
{
    /// <summary>
    /// Periodic-jitter waveform shape. These modulate the timing (phase) of the underlying clock/tone.
    /// Members keep their original numeric values so v1 projects load; <see cref="Custom"/> is appended.
    /// </summary>
    public enum JitterShape
    {
        /// <summary>No periodic jitter.</summary>
        None = 0,
        /// <summary>Sinusoidal jitter (the classic SJ tone used for jitter-tolerance masks).</summary>
        Sinusoidal = 1,
        /// <summary>Square-wave jitter (±peak displacement).</summary>
        Square = 2,
        /// <summary>Triangle-wave jitter.</summary>
        Triangle = 3,
        /// <summary>Saw-tooth (ramp) jitter.</summary>
        SawTooth = 4,
        /// <summary>Exponential-ramp jitter.</summary>
        Exponential = 5,
        /// <summary>A user-defined one-period profile from <see cref="JitterConfig.CustomShapeSamples"/>.</summary>
        Custom = 6
    }

    /// <summary>How the SJ frequency is swept across the record (jitter-tolerance-mask sweep).</summary>
    public enum JitterSweepMode
    {
        /// <summary>Sweep the SJ frequency linearly from start to stop.</summary>
        Linear = 0,
        /// <summary>Sweep the SJ frequency logarithmically from start to stop.</summary>
        Logarithmic = 1
    }

    /// <summary>A predefined or user-defined sinusoidal-jitter tolerance mask (amplitude vs frequency).</summary>
    public enum JitterMask
    {
        /// <summary>No mask (the sweep uses a constant amplitude).</summary>
        None = 0,
        /// <summary>User-defined breakpoints (<see cref="JitterConfig.CustomMaskFreqHz"/> / <see cref="JitterConfig.CustomMaskUiPp"/>).</summary>
        Custom = 1,
        /// <summary>ITU-T G.8251 OTU1 (~OC-48) tolerance mask — approximate corner values, see <see cref="JitterMasks"/>.</summary>
        G8251Oc48 = 2,
        /// <summary>ITU-T G.8251 OTU2 (~OC-192) tolerance mask — approximate corner values.</summary>
        G8251Oc192 = 3,
        /// <summary>ITU-T G.8251 OTU3 (~OC-768) tolerance mask — approximate corner values.</summary>
        G8251Oc768 = 4
    }

    /// <summary>
    /// Serializable settings for <see cref="JitterPersonality"/> — a jittered clock/tone stimulus for
    /// receiver jitter-tolerance testing (Signal Studio for Jitter Injection, E4438C-SP1). Jitter is
    /// applied as timing (phase) modulation of a sinusoidal clock; periodic and random jitter can be
    /// combined (composite). Amplitudes are expressed in unit intervals (UI), 1 UI = one clock period.
    /// </summary>
    /// <remarks>
    /// Covers periodic (sinusoidal/square/triangle/saw-tooth/exponential/<b>custom</b>) + random
    /// (Gaussian) + composite jitter with digital repeatability, plus (v2, #182) an <b>SJ frequency
    /// sweep</b> for building jitter-tolerance masks, predefined/custom <b>tolerance masks</b>, and
    /// <b>achievable-range enforcement</b>. The predefined ITU-T G.8251 masks use approximate corner
    /// values (see <see cref="JitterMasks"/>) and must be verified against the standard before compliance use.
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

        /// <summary>One-period custom jitter profile (normalized to [-1, +1]) used when
        /// <see cref="PeriodicShape"/> is <see cref="JitterShape.Custom"/>. Interpolated across the
        /// jitter period; null/empty behaves as no periodic jitter. (E4438C-SP1 R-2.)</summary>
        [DataMember] public double[] CustomShapeSamples { get; set; }

        /// <summary>When true, sweep the sinusoidal-jitter frequency across the record to trace a
        /// jitter-tolerance mask (overrides the fixed periodic shape). (E4438C-SP1 R-8.)</summary>
        [DataMember] public bool SweepEnabled { get; set; } = false;

        /// <summary>SJ sweep start frequency, in hertz.</summary>
        [DataMember] public double SweepStartHz { get; set; } = 1e3;

        /// <summary>SJ sweep stop frequency, in hertz.</summary>
        [DataMember] public double SweepStopHz { get; set; } = 1e6;

        /// <summary>How the SJ frequency is swept (linear or logarithmic).</summary>
        [DataMember] public JitterSweepMode SweepMode { get; set; } = JitterSweepMode.Logarithmic;

        /// <summary>Predefined/custom tolerance mask providing the SJ amplitude vs frequency. (E4438C-SP1 R-6.)</summary>
        [DataMember] public JitterMask MaskStandard { get; set; } = JitterMask.None;

        /// <summary>Breakpoint frequencies (Hz) for <see cref="JitterMask.Custom"/> (paired with
        /// <see cref="CustomMaskUiPp"/>, ascending frequency).</summary>
        [DataMember] public double[] CustomMaskFreqHz { get; set; }

        /// <summary>Breakpoint amplitudes (UI peak-to-peak) for <see cref="JitterMask.Custom"/>.</summary>
        [DataMember] public double[] CustomMaskUiPp { get; set; }

        /// <summary>When sweeping, take the SJ amplitude from <see cref="MaskStandard"/> at the current
        /// frequency (otherwise use the constant <see cref="PeriodicUiPp"/>). (E4438C-SP1 R-6/R-8.)</summary>
        [DataMember] public bool SweepFollowMask { get; set; } = false;

        /// <summary>Optional hardware cap on the total peak-to-peak jitter (UI). 0 = no explicit cap;
        /// otherwise the configured periodic amplitude must not exceed it. (E4438C-SP1 R-10.)</summary>
        [DataMember] public double MaxJitterUiPp { get; set; } = 0.0;
    }
}
