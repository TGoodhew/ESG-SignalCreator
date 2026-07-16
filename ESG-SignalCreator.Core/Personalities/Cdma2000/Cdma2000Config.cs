using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Cdma2000
{
    /// <summary>
    /// Serializable settings for <see cref="Cdma2000Personality"/> — a single-code 3GPP2 CDMA
    /// (cdma2000 / IS-95 / 1xEV-DO family) forward-link-style signal: QPSK data spread by a Walsh
    /// (OVSF) code, PN-scrambled, and pulse-shaped at the 1.2288 Mcps chip rate. Representative v1 core.
    /// </summary>
    [DataContract]
    public sealed class Cdma2000Config
    {
        /// <summary>Chip rate, in hertz. cdma2000 1x is 1.2288 Mcps.</summary>
        [DataMember] public double ChipRateHz { get; set; } = 1.2288e6;

        /// <summary>Oversampling: I/Q samples per chip. Sample rate = chip rate × this.</summary>
        [DataMember] public int SamplesPerChip { get; set; } = 4;

        /// <summary>Number of data symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 256;

        /// <summary>Walsh/OVSF spreading factor (power of two). cdma2000 uses 4…128.</summary>
        [DataMember] public int SpreadingFactor { get; set; } = 16;

        /// <summary>Walsh code index within the spreading factor (single-code mode).</summary>
        [DataMember] public int OvsfIndex { get; set; } = 1;

        /// <summary>Number of Walsh code channels to sum into a multi-channel forward-link composite
        /// (1 = single-code v1). Channels use Walsh codes 0…N-1 at equal power, N ≤ SF. (N7601B R-2.)</summary>
        [DataMember] public int CodeChannelCount { get; set; } = 1;

        /// <summary>Data modulation (cdma2000 forward traffic is QPSK).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QPSK;

        /// <summary>Baseband pulse-shaping roll-off (RRC approximation of the cdma2000 filter).</summary>
        [DataMember] public double RrcBeta { get; set; } = 0.3;

        /// <summary>Apply complex PN scrambling (stands in for the short/long PN codes).</summary>
        [DataMember] public bool Scramble { get; set; } = true;

        /// <summary>Scrambling PN seed.</summary>
        [DataMember] public int ScrambleSeed { get; set; } = 1;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
