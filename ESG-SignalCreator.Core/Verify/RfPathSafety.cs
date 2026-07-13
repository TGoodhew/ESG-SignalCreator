using System.Runtime.Serialization;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// Configuration for the RF-path input-damage safety gate.
    /// </summary>
    /// <remarks>
    /// In the hardware-in-the-loop setup an E4406A analyzer sits directly on the E4438C ESG's
    /// RF output. Because the application commands ESG output power, it could overdrive (and
    /// damage) the analyzer's RF input. This config describes the protected link so
    /// <see cref="PowerSafetyGate"/> can block any ESG power command that would exceed the
    /// analyzer's safe input level.
    /// </remarks>
    [DataContract]
    public sealed class RfPathSafety
    {
        /// <summary>
        /// True when the verification link is "armed" — i.e. the analyzer is known to be
        /// connected on the ESG output and must be protected. Defaults to false so the gate
        /// is inert until the analyzer is actually present.
        /// </summary>
        [DataMember]
        public bool Armed { get; set; } = false;

        /// <summary>
        /// Maximum safe RF input power at the analyzer, in dBm. Defaults to +30 dBm (the E4406A backstop,
        /// 5 dB below its +35 dBm rated max input). The connect dialog re-seeds this from the selected
        /// analyzer model via <see cref="AnalyzerInputLimits.DefaultMaxSafeInputDbm"/> (the N9010A default
        /// differs). Confirm against the specific analyzer unit's specifications.
        /// </summary>
        [DataMember]
        public double AnalyzerMaxSafeInputDbm { get; set; } = 30.0;

        /// <summary>
        /// Inline path loss between the ESG output and the analyzer input, in dB (e.g. a fixed
        /// pad or attenuator). A direct cable is 0 dB, which is the conservative default — it
        /// assumes the full ESG power reaches the analyzer.
        /// </summary>
        [DataMember]
        public double PathLossDb { get; set; } = 0.0;
    }
}
