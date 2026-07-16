using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.WimaxFixed
{
    /// <summary>Cyclic-prefix ratio (G) options for 802.16 OFDM.</summary>
    public enum CpRatio
    {
        /// <summary>1/4 of the symbol.</summary>
        OneQuarter,
        /// <summary>1/8 of the symbol.</summary>
        OneEighth,
        /// <summary>1/16 of the symbol.</summary>
        OneSixteenth,
        /// <summary>1/32 of the symbol.</summary>
        OneThirtySecond
    }

    /// <summary>
    /// Serializable settings for <see cref="WimaxFixedPersonality"/> — a fixed-WiMAX (IEEE 802.16-2004)
    /// **256-FFT OFDM** signal (200 used subcarriers: 192 data + 8 pilots + DC null). Sample rate is
    /// derived from the channel bandwidth via the 8/7 sampling factor. Representative v1 core.
    /// </summary>
    [DataContract]
    public sealed class WimaxFixedConfig
    {
        /// <summary>Nominal channel bandwidth, in hertz (sample rate ≈ bandwidth × 8/7).</summary>
        [DataMember] public double ChannelBandwidthHz { get; set; } = 3.5e6;

        /// <summary>Cyclic-prefix ratio (G).</summary>
        [DataMember] public CpRatio CyclicPrefixRatio { get; set; } = CpRatio.OneEighth;

        /// <summary>Number of OFDM symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 32;

        /// <summary>Subcarrier modulation (BPSK/QPSK/16QAM/64QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM16;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;

        /// <summary>When true, build a frame with the exact 256-FFT pilot map and an optional preamble,
        /// rather than the generic OFDM fill. (N7613A R-2 pilots / R-1 & R-7 preamble.)</summary>
        [DataMember] public bool FrameStructured { get; set; } = false;

        /// <summary>When true (frame-structured mode), prepend the downlink preamble symbol. (N7613A R-7.)</summary>
        [DataMember] public bool IncludePreamble { get; set; } = true;
    }
}
