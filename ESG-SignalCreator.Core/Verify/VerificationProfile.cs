using System.Runtime.Serialization;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// Per-metric tolerances and path corrections for the in-app closed-loop verification
    /// (UX/E4406A doc §7.2). Defaults match the bench-validated headless harness.
    /// </summary>
    [DataContract]
    public sealed class VerificationProfile
    {
        /// <summary>Channel-power tolerance, in dB (default ±3 dB; covers cable loss + analyzer accuracy).</summary>
        [DataMember] public double PowerToleranceDb { get; set; } = 3.0;

        /// <summary>PAPR (crest-factor) tolerance, in dB (default ±2.5 dB).</summary>
        [DataMember] public double PaprToleranceDb { get; set; } = 2.5;

        /// <summary>Tone-frequency tolerance, in hertz (default 50 kHz; a fraction of the RBW).</summary>
        [DataMember] public double FrequencyToleranceHz { get; set; } = 50e3;

        /// <summary>Inline path loss between the ESG output and the analyzer input, in dB.</summary>
        [DataMember] public double PathLossDb { get; set; }

        /// <summary>Measurement span used for channel power / spectrum, in hertz.</summary>
        [DataMember] public double MeasurementSpanHz { get; set; } = 5e6;
    }
}
