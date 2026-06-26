using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.CustomMod
{
    /// <summary>
    /// Digital modulation scheme selecting the symbol constellation.
    /// </summary>
    public enum Modulation
    {
        /// <summary>Binary phase-shift keying: two points (±1) on the I axis.</summary>
        BPSK,

        /// <summary>Quadrature PSK: four points (±1±j)/√2, Gray-coded.</summary>
        QPSK,

        /// <summary>8-PSK: eight equally-spaced points on the unit circle, Gray-coded.</summary>
        PSK8,

        /// <summary>16-QAM: 4×4 square grid, Gray-coded, unit average power.</summary>
        QAM16,

        /// <summary>64-QAM: 8×8 square grid, Gray-coded, unit average power.</summary>
        QAM64,

        /// <summary>256-QAM: 16×16 square grid, Gray-coded, unit average power.</summary>
        QAM256,

        /// <summary>Minimum-shift keying: constant-envelope, generated as offset-QPSK with half-sine shaping.</summary>
        MSK
    }

    /// <summary>
    /// Baseband pulse-shaping filter applied across symbols.
    /// </summary>
    public enum PulseShape
    {
        /// <summary>Root-raised-cosine (matched-filter pair). Uses <see cref="CustomModConfig.Alpha"/> as roll-off.</summary>
        RootRaisedCosine,

        /// <summary>Raised-cosine (zero-ISI at symbol instants). Uses <see cref="CustomModConfig.Alpha"/> as roll-off.</summary>
        RaisedCosine,

        /// <summary>Gaussian. Uses <see cref="CustomModConfig.Alpha"/> as the bandwidth-time (BT) product.</summary>
        Gaussian,

        /// <summary>Rectangular (sample-and-hold): each symbol held for <see cref="CustomModConfig.SamplesPerSymbol"/> samples.</summary>
        Rectangular
    }

    /// <summary>
    /// Pseudo-random / fixed data source feeding the symbol mapper, MSB-first.
    /// </summary>
    public enum DataSource
    {
        /// <summary>PN9 maximal-length LFSR (period 511).</summary>
        PN9,

        /// <summary>PN15 maximal-length LFSR (period 32767).</summary>
        PN15,

        /// <summary>PN23 maximal-length LFSR (period 8388607).</summary>
        PN23,

        /// <summary>Constant 0 bits.</summary>
        AllZeros,

        /// <summary>Constant 1 bits.</summary>
        AllOnes
    }

    /// <summary>
    /// Serializable settings for <see cref="CustomModPersonality"/>: a digitally-modulated
    /// baseband I/Q source built from a chosen constellation, symbol rate, pulse-shaping
    /// filter and data source.
    /// </summary>
    [DataContract]
    public sealed class CustomModConfig
    {
        /// <summary>Constellation / modulation scheme.</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QPSK;

        /// <summary>Symbol rate, in symbols per second.</summary>
        [DataMember] public double SymbolRateHz { get; set; } = 1e6;

        /// <summary>Oversampling factor: I/Q samples generated per symbol.</summary>
        [DataMember] public int SamplesPerSymbol { get; set; } = 8;

        /// <summary>Baseband pulse-shaping filter.</summary>
        [DataMember] public PulseShape Shape { get; set; } = PulseShape.RootRaisedCosine;

        /// <summary>Roll-off factor α (in [0,1]) for RRC/RC, or the BT product for Gaussian.</summary>
        [DataMember] public double Alpha { get; set; } = 0.35;

        /// <summary>Pulse-shaping filter span, in symbols.</summary>
        [DataMember] public int FilterSpanSymbols { get; set; } = 8;

        /// <summary>Number of symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 512;

        /// <summary>Data source feeding the symbol mapper (MSB-first).</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
