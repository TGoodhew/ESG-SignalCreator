using System.Runtime.Serialization;
using EsgSignalCreator.Io;

namespace EsgSignalCreator.Personalities.CustomIq
{
    /// <summary>
    /// Settings for the <see cref="ImportIqPersonality"/>: which file to load and how to interpret it.
    /// </summary>
    [DataContract]
    public sealed class ImportIqConfig
    {
        /// <summary>Path to the I/Q file (CSV/TSV/TXT, raw int16 .bin/.iq, Agilent big-endian int16 .agt, or 16-bit PCM .wav).</summary>
        [DataMember(Name = "path")]
        public string Path { get; set; }

        /// <summary>
        /// Container format. <see cref="IqFileReader.IqFormat.Auto"/> detects from the file extension;
        /// set explicitly (e.g. <see cref="IqFileReader.IqFormat.AgilentInt16Be"/>) when the extension
        /// is ambiguous or you need to force the Agilent/Keysight big-endian byte order.
        /// </summary>
        [DataMember(Name = "format")]
        public IqFileReader.IqFormat Format { get; set; } = IqFileReader.IqFormat.Auto;

        /// <summary>
        /// Sample rate to use for formats that carry no rate (CSV/TSV, raw int16). Ignored for WAV
        /// unless positive, in which case it overrides the header rate. Default 10 MHz.
        /// </summary>
        [DataMember(Name = "sampleRateHz")]
        public double SampleRateHz { get; set; } = 10e6;

        /// <summary>Swap the I and Q channels on load.</summary>
        [DataMember(Name = "swapIq")]
        public bool SwapIq { get; set; } = false;

        /// <summary>Multiplier applied to every sample on load.</summary>
        [DataMember(Name = "scale")]
        public double Scale { get; set; } = 1.0;
    }
}
