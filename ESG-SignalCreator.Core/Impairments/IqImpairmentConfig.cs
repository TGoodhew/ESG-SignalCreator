using System.Runtime.Serialization;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Modulator I/Q impairments applied to a baseband waveform: gain imbalance, quadrature
    /// (phase) skew, DC offsets, and an optional I/Q channel swap. All values default to a no-op
    /// so a freshly constructed config leaves the waveform unchanged.
    /// </summary>
    [DataContract]
    public sealed class IqImpairmentConfig
    {
        /// <summary>
        /// Amplitude imbalance between the I and Q channels, in dB. The I channel is scaled by
        /// +half this value and Q by -half, so 0 dB leaves both unchanged.
        /// </summary>
        [DataMember]
        public double GainImbalanceDb { get; set; } = 0;

        /// <summary>
        /// Quadrature skew (departure from a perfect 90° I/Q phase relationship), in degrees.
        /// Zero leaves the channels orthogonal.
        /// </summary>
        [DataMember]
        public double QuadratureSkewDeg { get; set; } = 0;

        /// <summary>DC offset added to the I channel, in normalized units.</summary>
        [DataMember]
        public double DcOffsetI { get; set; } = 0;

        /// <summary>DC offset added to the Q channel, in normalized units.</summary>
        [DataMember]
        public double DcOffsetQ { get; set; } = 0;

        /// <summary>When true, exchange the I and Q channels after all other impairments.</summary>
        [DataMember]
        public bool SwapIq { get; set; } = false;
    }
}
