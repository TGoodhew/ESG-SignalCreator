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

    /// <summary>Cyclic-prefix mode for the E-UTRA downlink (normal = 7 symbols/slot, extended = 6).</summary>
    public enum LteCyclicPrefix
    {
        /// <summary>Normal CP — 7 OFDM symbols per 0.5 ms slot.</summary>
        Normal = 0,
        /// <summary>Extended CP — 6 OFDM symbols per slot (longer prefix, for large delay spread).</summary>
        Extended = 1
    }

    /// <summary>
    /// Serializable settings for the LTE personalities (FDD/TDD). In the default (v1) mode it generates
    /// a generic downlink OFDM signal with LTE numerology (15 kHz spacing, standard FFT/CP per
    /// bandwidth). With <see cref="FrameStructured"/> set, the FDD personality instead builds a proper
    /// E-UTRA downlink radio-frame (v2, #188): 10 ms frame / 0.5 ms slots / per-symbol CP, with
    /// correctly-positioned PSS, SSS, and cell-specific reference signals (CRS, antenna port 0) and a
    /// PDSCH data fill on the remaining resource elements.
    /// </summary>
    [DataContract]
    public sealed class LteConfig
    {
        /// <summary>Channel bandwidth (selects FFT size and occupied subcarriers).</summary>
        [DataMember] public LteBandwidth Bandwidth { get; set; } = LteBandwidth.Bw10MHz;

        /// <summary>Number of OFDM symbols to generate in the generic (non-frame-structured) mode.</summary>
        [DataMember] public int SymbolCount { get; set; } = 14;

        /// <summary>Subcarrier / PDSCH modulation (QPSK / 16QAM / 64QAM / 256QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM16;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;

        /// <summary>When true (FDD), build a proper E-UTRA downlink frame (PSS/SSS/CRS + PDSCH) rather
        /// than the generic OFDM fill. (N7624B R-6.)</summary>
        [DataMember] public bool FrameStructured { get; set; } = false;

        /// <summary>Cyclic-prefix mode (frame-structured mode). (N7624B R-2.)</summary>
        [DataMember] public LteCyclicPrefix CyclicPrefix { get; set; } = LteCyclicPrefix.Normal;

        /// <summary>Physical-layer cell identity N_cell_ID (0..503) — drives PSS/SSS and the CRS
        /// frequency shift/sequence in frame-structured mode.</summary>
        [DataMember] public int PhysicalCellId { get; set; } = 0;

        /// <summary>Number of 1 ms subframes to generate in frame-structured mode (10 = one radio frame).</summary>
        [DataMember] public int SubframeCount { get; set; } = 10;
    }
}
