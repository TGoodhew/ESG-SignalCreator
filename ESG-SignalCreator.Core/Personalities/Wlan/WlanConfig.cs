using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Wlan
{
    /// <summary>802.11 OFDM channel bandwidth (selects FFT size, used subcarriers, and CP).</summary>
    public enum WlanBandwidth
    {
        /// <summary>20 MHz (802.11a/g/n): 64-point FFT, 52 used subcarriers, 16-sample CP.</summary>
        Bw20MHz,
        /// <summary>40 MHz (802.11n): 128-point FFT, 108 used subcarriers, 32-sample CP.</summary>
        Bw40MHz
    }

    /// <summary>Data-symbol guard interval (cyclic prefix) for the frame-structured 802.11a/g PPDU.</summary>
    public enum WlanGuardInterval
    {
        /// <summary>Long GI — 0.8 µs (16 samples at 20 MHz), the 802.11a/g default.</summary>
        Long = 0,
        /// <summary>Short GI — 0.4 µs (8 samples), the 802.11n optional short guard interval.</summary>
        Short = 1
    }

    /// <summary>
    /// Serializable settings for <see cref="WlanPersonality"/>. In the default (v1) mode it generates a
    /// generic 802.11 OFDM signal (11a/g/n-style) at 312.5 kHz subcarrier spacing. With
    /// <see cref="FrameStructured"/> set (20 MHz only), it builds a representative 802.11a/g <b>PPDU</b>
    /// (v2, #191): an optional <b>L-LTF</b> training preamble followed by data OFDM symbols that carry
    /// the four <b>pilot subcarriers</b> (±7, ±21) with the standard polarity, using a selectable
    /// <b>guard interval</b>.
    /// </summary>
    [DataContract]
    public sealed class WlanConfig
    {
        /// <summary>Channel bandwidth (selects FFT size / used subcarriers / CP).</summary>
        [DataMember] public WlanBandwidth Bandwidth { get; set; } = WlanBandwidth.Bw20MHz;

        /// <summary>Number of OFDM symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 32;

        /// <summary>Subcarrier modulation (BPSK…256QAM depending on MCS).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM64;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;

        /// <summary>When true (20 MHz), build a representative 802.11a/g PPDU with pilots + an optional
        /// L-LTF preamble rather than the generic OFDM fill. (N7617B R-10 / R-3.)</summary>
        [DataMember] public bool FrameStructured { get; set; } = false;

        /// <summary>Data-symbol guard interval used in frame-structured mode. (N7617B R-10.)</summary>
        [DataMember] public WlanGuardInterval GuardInterval { get; set; } = WlanGuardInterval.Long;

        /// <summary>When true (frame-structured mode), prepend the L-LTF training preamble. (N7617B R-3.)</summary>
        [DataMember] public bool IncludeLtfPreamble { get; set; } = true;
    }
}
