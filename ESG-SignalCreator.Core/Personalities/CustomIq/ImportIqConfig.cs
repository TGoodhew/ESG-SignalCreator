using System.Runtime.Serialization;
using EsgSignalCreator.Io;

namespace EsgSignalCreator.Personalities.CustomIq
{
    /// <summary>How markers are authored onto an imported ARB segment (N7622A R-7).</summary>
    public enum ImportMarkerMode
    {
        /// <summary>No markers are attached.</summary>
        None = 0,
        /// <summary>A single one-sample marker at the start of the segment (a segment trigger).</summary>
        Start = 1,
        /// <summary>A one-sample marker every <see cref="ImportIqConfig.MarkerPeriodSamples"/> samples.</summary>
        Periodic = 2,
        /// <summary>A contiguous marker block of <see cref="ImportIqConfig.MarkerLengthSamples"/> samples
        /// starting at <see cref="ImportIqConfig.MarkerStartSample"/> (a gate over part of the segment).</summary>
        Range = 3
    }

    /// <summary>
    /// Settings for the <see cref="ImportIqPersonality"/>: which file to load, how to interpret it,
    /// and optional marker/trigger authoring on the resulting ARB segment.
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

        /// <summary>Marker/trigger authoring mode for the imported segment (N7622A R-7).</summary>
        [DataMember(Name = "markerMode")]
        public ImportMarkerMode MarkerMode { get; set; } = ImportMarkerMode.None;

        /// <summary>Marker period, in samples, for <see cref="ImportMarkerMode.Periodic"/> (>= 1).</summary>
        [DataMember(Name = "markerPeriodSamples")]
        public int MarkerPeriodSamples { get; set; } = 1000;

        /// <summary>First sample of the marker block for <see cref="ImportMarkerMode.Range"/> (>= 0).</summary>
        [DataMember(Name = "markerStartSample")]
        public int MarkerStartSample { get; set; } = 0;

        /// <summary>Length, in samples, of the marker block for <see cref="ImportMarkerMode.Range"/> (>= 1).</summary>
        [DataMember(Name = "markerLengthSamples")]
        public int MarkerLengthSamples { get; set; } = 1;
    }
}
