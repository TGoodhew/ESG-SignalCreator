using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Hspa
{
    /// <summary>
    /// Serializable settings for <see cref="HspaPersonality"/> — a single-code 3GPP W-CDMA HSPA
    /// (HS-PDSCH-style) signal: QPSK or 16QAM data spread by an SF-16 OVSF code, complex-scrambled,
    /// and RRC-shaped at 3.84 Mcps. A representative v1 core, not a standards-compliant HSDPA/HSUPA link.
    /// </summary>
    [DataContract]
    public sealed class HspaConfig
    {
        /// <summary>Chip rate, in hertz. W-CDMA/HSPA is 3.84 Mcps.</summary>
        [DataMember] public double ChipRateHz { get; set; } = 3.84e6;

        /// <summary>Oversampling: I/Q samples per chip. Sample rate = chip rate × this.</summary>
        [DataMember] public int SamplesPerChip { get; set; } = 4;

        /// <summary>Number of data symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 512;

        /// <summary>OVSF spreading factor. HS-PDSCH uses SF = 16.</summary>
        [DataMember] public int SpreadingFactor { get; set; } = 16;

        /// <summary>OVSF code index within the spreading factor (single-code mode).</summary>
        [DataMember] public int OvsfIndex { get; set; } = 1;

        /// <summary>Number of HS-PDSCH multicodes to sum into the composite (1 = single-code v1). HSDPA
        /// uses several SF-16 codes; channels use OVSF codes 0…N-1 at equal power, N ≤ SF. (E4438C-419 R-2.)</summary>
        [DataMember] public int CodeChannelCount { get; set; } = 1;

        /// <summary>HS-PDSCH modulation: QPSK or 16QAM (64QAM in later releases).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM16;

        /// <summary>Root-raised-cosine roll-off. W-CDMA/HSPA uses 0.22.</summary>
        [DataMember] public double RrcBeta { get; set; } = 0.22;

        /// <summary>Apply complex PN scrambling.</summary>
        [DataMember] public bool Scramble { get; set; } = true;

        /// <summary>Scrambling PN seed.</summary>
        [DataMember] public int ScrambleSeed { get; set; } = 1;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
