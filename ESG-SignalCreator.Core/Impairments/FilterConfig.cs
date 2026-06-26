using System.Runtime.Serialization;

namespace EsgSignalCreator.Impairments
{
    /// <summary>Frequency response shape requested from <see cref="FilterImpairment"/>.</summary>
    public enum FilterType
    {
        /// <summary>Passes frequencies below the cutoff; attenuates those above.</summary>
        LowPass,

        /// <summary>Passes frequencies above the cutoff; attenuates those below.</summary>
        HighPass,

        /// <summary>Passes a band centred on the cutoff with the configured bandwidth.</summary>
        BandPass
    }

    /// <summary>
    /// Settings for the inline FIR filter / correction impairment. A windowed-sinc (Hann)
    /// linear-phase FIR is designed from these parameters at the waveform's own sample rate and
    /// applied to both I and Q rails. All frequencies are baseband (relative to DC / the carrier).
    /// </summary>
    [DataContract]
    public sealed class FilterConfig
    {
        /// <summary>Frequency-response shape. Defaults to <see cref="FilterType.LowPass"/>.</summary>
        [DataMember]
        public FilterType Type { get; set; } = FilterType.LowPass;

        /// <summary>
        /// Cutoff frequency, in hertz. For low-pass and high-pass this is the −6 dB edge.
        /// For band-pass this is the centre frequency of the pass band.
        /// </summary>
        [DataMember]
        public double CutoffHz { get; set; } = 2e6;

        /// <summary>
        /// Pass-band width in hertz, used only for <see cref="FilterType.BandPass"/>: the band
        /// spans <c>CutoffHz ± BandwidthHz/2</c>.
        /// </summary>
        [DataMember]
        public double BandwidthHz { get; set; } = 1e6;

        /// <summary>
        /// Number of FIR taps. Forced odd internally so the filter has a single centre tap and is
        /// exactly symmetric (linear phase). Defaults to 65.
        /// </summary>
        [DataMember]
        public int Taps { get; set; } = 65;
    }
}
