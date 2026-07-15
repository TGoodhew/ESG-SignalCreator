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

    /// <summary>
    /// Serializable settings for <see cref="WlanPersonality"/> — a generic 802.11 OFDM signal
    /// (11a/g/n-style) at 312.5 kHz subcarrier spacing. A representative v1 core, not a
    /// standards-compliant PPDU (no preamble/SIG fields, pilots, coding, or MIMO).
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
    }
}
