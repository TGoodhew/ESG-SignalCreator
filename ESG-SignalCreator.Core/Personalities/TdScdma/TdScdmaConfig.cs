using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.TdScdma
{
    /// <summary>
    /// Serializable settings for <see cref="TdScdmaPersonality"/> — a single-code TD-SCDMA
    /// (1.28 Mcps low-chip-rate TDD) signal: QPSK/16QAM/64QAM data spread by an OVSF code,
    /// scrambled, and RRC-shaped (β = 0.22). Representative v1 core, not a standards-compliant link.
    /// </summary>
    [DataContract]
    public sealed class TdScdmaConfig
    {
        /// <summary>Chip rate, in hertz. TD-SCDMA is 1.28 Mcps.</summary>
        [DataMember] public double ChipRateHz { get; set; } = 1.28e6;

        /// <summary>Oversampling: I/Q samples per chip. Sample rate = chip rate × this.</summary>
        [DataMember] public int SamplesPerChip { get; set; } = 8;

        /// <summary>Number of data symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 256;

        /// <summary>OVSF spreading factor (power of two). TD-SCDMA uses 1…16.</summary>
        [DataMember] public int SpreadingFactor { get; set; } = 16;

        /// <summary>OVSF code index within the spreading factor.</summary>
        [DataMember] public int OvsfIndex { get; set; } = 1;

        /// <summary>Data modulation (TD-SCDMA supports QPSK / 16QAM / 64QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QPSK;

        /// <summary>Root-raised-cosine roll-off. TD-SCDMA uses 0.22.</summary>
        [DataMember] public double RrcBeta { get; set; } = 0.22;

        /// <summary>Apply complex PN scrambling.</summary>
        [DataMember] public bool Scramble { get; set; } = true;

        /// <summary>Scrambling PN seed.</summary>
        [DataMember] public int ScrambleSeed { get; set; } = 1;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
