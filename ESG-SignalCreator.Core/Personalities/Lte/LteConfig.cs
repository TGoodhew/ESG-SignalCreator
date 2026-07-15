using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Lte
{
    /// <summary>
    /// LTE channel bandwidth, with its standard FFT size and occupied (used) subcarrier count at the
    /// 15 kHz subcarrier spacing. Occupied carriers = resource blocks × 12.
    /// </summary>
    public enum LteBandwidth
    {
        /// <summary>1.4 MHz — 6 RB, FFT 128, 72 subcarriers.</summary>
        Bw1_4MHz,
        /// <summary>3 MHz — 15 RB, FFT 256, 180 subcarriers.</summary>
        Bw3MHz,
        /// <summary>5 MHz — 25 RB, FFT 512, 300 subcarriers.</summary>
        Bw5MHz,
        /// <summary>10 MHz — 50 RB, FFT 1024, 600 subcarriers.</summary>
        Bw10MHz,
        /// <summary>15 MHz — 75 RB, FFT 1536 (uses 2048 here), 900 subcarriers.</summary>
        Bw15MHz,
        /// <summary>20 MHz — 100 RB, FFT 2048, 1200 subcarriers.</summary>
        Bw20MHz
    }

    /// <summary>
    /// Serializable settings for the LTE personalities (FDD/TDD). Generates a generic downlink OFDM
    /// signal with LTE numerology (15 kHz spacing, standard FFT/CP per bandwidth). A representative
    /// v1 core, not a standards-compliant LTE frame.
    /// </summary>
    [DataContract]
    public sealed class LteConfig
    {
        /// <summary>Channel bandwidth (selects FFT size and occupied subcarriers).</summary>
        [DataMember] public LteBandwidth Bandwidth { get; set; } = LteBandwidth.Bw10MHz;

        /// <summary>Number of OFDM symbols to generate (one slot = 7 with normal CP).</summary>
        [DataMember] public int SymbolCount { get; set; } = 14;

        /// <summary>Subcarrier modulation (QPSK / 16QAM / 64QAM / 256QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM16;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
