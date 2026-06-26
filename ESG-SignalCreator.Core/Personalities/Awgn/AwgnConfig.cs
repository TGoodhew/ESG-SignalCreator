using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.Awgn
{
    /// <summary>
    /// Settings for the AWGN (band-limited additive white Gaussian noise) personality.
    /// Complex Gaussian noise is generated, optionally low-pass filtered to a chosen noise
    /// bandwidth, clipped to a target crest factor, and finally normalized so its peak vector
    /// magnitude is 1.0.
    /// </summary>
    [DataContract]
    public sealed class AwgnConfig
    {
        /// <summary>I/Q playback (sample clock) rate, in hertz.</summary>
        [DataMember]
        public double SampleRateHz { get; set; } = 10e6;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember]
        public int Length { get; set; } = 32768;

        /// <summary>
        /// Two-sided bandwidth of the noise, in hertz. If this is greater than or equal to
        /// <see cref="SampleRateHz"/>, the noise occupies the full band and no band-limiting
        /// filter is applied.
        /// </summary>
        [DataMember]
        public double NoiseBandwidthHz { get; set; } = 5e6;

        /// <summary>
        /// Crest factor (peak-to-RMS of the complex envelope) cap, in dB. The complex envelope
        /// magnitude is clipped at this many dB above its RMS. A value of zero or less disables
        /// clipping.
        /// </summary>
        [DataMember]
        public double CrestFactorDb { get; set; } = 12;

        /// <summary>Seed for the deterministic Gaussian noise generator.</summary>
        [DataMember]
        public int RandomSeed { get; set; } = 12345;
    }
}
