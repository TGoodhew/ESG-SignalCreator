using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.WimaxMobile
{
    /// <summary>Scalable-OFDMA FFT size for mobile WiMAX (802.16e), tied to channel bandwidth.</summary>
    public enum WimaxFftSize
    {
        /// <summary>128-point FFT (≈ 1.25 MHz).</summary>
        Fft128,
        /// <summary>512-point FFT (≈ 5 MHz).</summary>
        Fft512,
        /// <summary>1024-point FFT (≈ 10 MHz).</summary>
        Fft1024,
        /// <summary>2048-point FFT (≈ 20 MHz).</summary>
        Fft2048
    }

    /// <summary>
    /// Serializable settings for <see cref="WimaxMobilePersonality"/> — a mobile-WiMAX (IEEE 802.16e)
    /// **scalable-OFDMA** signal at the fixed 10.9375 kHz subcarrier spacing (sample rate scales with
    /// FFT size). Modelled as plain OFDM (no OFDMA subchannel permutation). Representative v1 core.
    /// </summary>
    [DataContract]
    public sealed class WimaxMobileConfig
    {
        /// <summary>Scalable FFT size (selects channel bandwidth and sample rate at 10.9375 kHz spacing).</summary>
        [DataMember] public WimaxFftSize FftSize { get; set; } = WimaxFftSize.Fft1024;

        /// <summary>Cyclic-prefix ratio (G).</summary>
        [DataMember] public CpRatio CyclicPrefixRatio { get; set; } = CpRatio.OneEighth;

        /// <summary>Number of OFDM symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 24;

        /// <summary>Subcarrier modulation (QPSK/16QAM/64QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM16;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;

        /// <summary>When true, build a DL-OFDMA-style frame with a preamble and a DL-PUSC pilot pattern
        /// rather than the generic OFDM fill. (N7615B R-1/R-6 frame, R-4 pilots.)</summary>
        [DataMember] public bool FrameStructured { get; set; } = false;

        /// <summary>When true (frame-structured mode), prepend the OFDMA downlink preamble symbol. (N7615B R-6.)</summary>
        [DataMember] public bool IncludePreamble { get; set; } = true;
    }
}
