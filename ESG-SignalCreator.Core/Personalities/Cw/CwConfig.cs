using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Cw
{
    /// <summary>
    /// Settings for the CW / single-tone personality. A constant-envelope complex sinusoid at a
    /// fixed baseband frequency offset, used as a clean carrier (or DC when the offset is zero).
    /// </summary>
    [DataContract]
    public sealed class CwConfig
    {
        /// <summary>I/Q playback (sample clock) rate, in hertz.</summary>
        [DataMember]
        public double SampleRateHz { get; set; } = 10e6;

        /// <summary>
        /// Desired complex sample count. The actual length is nudged to the nearest value that holds
        /// a whole number of cycles of the offset tone, so the segment loops seamlessly.
        /// </summary>
        [DataMember]
        public int Length { get; set; } = 4000;

        /// <summary>Baseband frequency offset of the tone, in hertz. Zero gives DC.</summary>
        [DataMember]
        public double FreqOffsetHz { get; set; } = 100e3;

        /// <summary>Envelope amplitude (vector magnitude), normally ≤ 1.0.</summary>
        [DataMember]
        public double AmplitudeScale { get; set; } = 1.0;

        /// <summary>Starting phase of the tone, in degrees.</summary>
        [DataMember]
        public double PhaseDeg { get; set; } = 0;
    }
}
