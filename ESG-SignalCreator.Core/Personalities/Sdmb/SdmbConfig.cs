using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Sdmb
{
    /// <summary>
    /// Serializable settings for <see cref="SdmbPersonality"/> — an approximate Satellite-DMB (System E,
    /// CDM) stimulus: QPSK data spread by an OVSF code, scrambled, and RRC-shaped. The S-DMB air-interface
    /// physical layer could not be confirmed from primary literature (see the E4438C-407 requirements doc),
    /// so this is a representative CDM signal, NOT a verified S-DMB waveform.
    /// </summary>
    [DataContract]
    public sealed class SdmbConfig
    {
        /// <summary>Chip rate, in hertz (CDM approximation — unverified for S-DMB System E).</summary>
        [DataMember] public double ChipRateHz { get; set; } = 1.2288e6;

        /// <summary>Oversampling: I/Q samples per chip. Sample rate = chip rate × this.</summary>
        [DataMember] public int SamplesPerChip { get; set; } = 4;

        /// <summary>Number of data symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 256;

        /// <summary>OVSF spreading factor (power of two).</summary>
        [DataMember] public int SpreadingFactor { get; set; } = 16;

        /// <summary>OVSF code index within the spreading factor.</summary>
        [DataMember] public int OvsfIndex { get; set; } = 1;

        /// <summary>Data modulation (QPSK).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QPSK;

        /// <summary>Root-raised-cosine roll-off.</summary>
        [DataMember] public double RrcBeta { get; set; } = 0.22;

        /// <summary>Apply complex PN scrambling.</summary>
        [DataMember] public bool Scramble { get; set; } = true;

        /// <summary>Scrambling PN seed.</summary>
        [DataMember] public int ScrambleSeed { get; set; } = 1;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
