using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Wcdma
{
    /// <summary>
    /// Serializable settings for <see cref="WcdmaPersonality"/> — a single-code 3GPP W-CDMA (FDD)
    /// downlink-style signal: QPSK data spread by an OVSF code, complex-scrambled, and RRC-shaped at
    /// the 3.84 Mcps chip rate. A representative v1 core, not a standards-compliant multi-code downlink.
    /// </summary>
    [DataContract]
    public sealed class WcdmaConfig
    {
        /// <summary>Chip rate, in hertz. 3GPP W-CDMA is 3.84 Mcps.</summary>
        [DataMember] public double ChipRateHz { get; set; } = 3.84e6;

        /// <summary>Oversampling: I/Q samples per chip. Sample rate = chip rate × this.</summary>
        [DataMember] public int SamplesPerChip { get; set; } = 4;

        /// <summary>Number of data symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 256;

        /// <summary>OVSF spreading factor (power of two). W-CDMA DL uses 4…512.</summary>
        [DataMember] public int SpreadingFactor { get; set; } = 16;

        /// <summary>OVSF code index within the spreading factor.</summary>
        [DataMember] public int OvsfIndex { get; set; } = 1;

        /// <summary>Modulation of the data symbols before spreading (W-CDMA DPCH is QPSK).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QPSK;

        /// <summary>Root-raised-cosine roll-off. W-CDMA uses 0.22.</summary>
        [DataMember] public double RrcBeta { get; set; } = 0.22;

        /// <summary>Apply complex PN scrambling.</summary>
        [DataMember] public bool Scramble { get; set; } = true;

        /// <summary>Scrambling PN seed (stands in for the cell scrambling code).</summary>
        [DataMember] public int ScrambleSeed { get; set; } = 1;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
