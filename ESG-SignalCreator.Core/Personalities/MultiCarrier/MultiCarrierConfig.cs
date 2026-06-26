using System;
using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.MultiCarrier
{
    /// <summary>
    /// A single carrier in a multi-carrier signal: a frequency offset from baseband centre,
    /// a relative power, an explicit starting phase, a per-carrier circular time shift, and an
    /// enable flag so carriers can be toggled without removing them.
    /// </summary>
    [DataContract]
    public sealed class Carrier
    {
        /// <summary>Frequency offset from baseband centre, in hertz (may be negative).</summary>
        [DataMember] public double FreqOffsetHz { get; set; }

        /// <summary>Relative carrier power in dB; linear amplitude is 10^(PowerDb/20).</summary>
        [DataMember] public double PowerDb { get; set; }

        /// <summary>Starting phase of the carrier, in degrees.</summary>
        [DataMember] public double PhaseDeg { get; set; }

        /// <summary>
        /// Per-carrier delay, in samples, applied circularly (wrapped modulo the waveform length).
        /// Positive values delay the carrier (shift later in time); negative values advance it.
        /// </summary>
        [DataMember] public int DelaySamples { get; set; }

        /// <summary>When false, the carrier is skipped during calculation.</summary>
        [DataMember] public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Serializable settings for <see cref="MultiCarrierPersonality"/>.
    /// </summary>
    [DataContract]
    public sealed class MultiCarrierConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 10e6;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember] public int Length { get; set; } = 16384;

        /// <summary>The carriers making up the signal. Disabled carriers are ignored.</summary>
        [DataMember] public Carrier[] Carriers { get; set; } = Array.Empty<Carrier>();
    }
}
