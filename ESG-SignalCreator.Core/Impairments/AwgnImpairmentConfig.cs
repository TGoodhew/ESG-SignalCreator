using System.Runtime.Serialization;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Settings for the AWGN inline impairment: additive white Gaussian noise added to an
    /// already-built waveform at a target carrier-to-noise ratio.
    /// </summary>
    /// <remarks>
    /// Unlike the AWGN <em>personality</em> (which synthesizes a standalone noise waveform),
    /// this impairment measures the average power of an existing signal and adds complex
    /// Gaussian noise scaled so the signal-power-to-noise-power ratio equals
    /// <see cref="CarrierToNoiseDb"/>.
    /// </remarks>
    [DataContract]
    public sealed class AwgnImpairmentConfig
    {
        /// <summary>
        /// Target carrier-to-noise ratio, in dB: the ratio of the input signal's average power
        /// to the average power of the added noise. Higher values add less noise.
        /// </summary>
        [DataMember]
        public double CarrierToNoiseDb { get; set; } = 20.0;

        /// <summary>Seed for the deterministic Gaussian noise generator.</summary>
        [DataMember]
        public int RandomSeed { get; set; } = 12345;

        /// <summary>
        /// When true (default), the signal+noise sum is rescaled so its peak vector magnitude
        /// is exactly 1.0 after noise is added.
        /// </summary>
        [DataMember]
        public bool RenormalizePeak { get; set; } = true;
    }
}
