using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Tdmb
{
    /// <summary>DAB/T-DMB transmission mode (sets FFT size, carriers, guard interval, spacing).</summary>
    public enum DabMode
    {
        /// <summary>Mode I — 2048-FFT, 1536 carriers, 1 kHz spacing.</summary>
        ModeI,
        /// <summary>Mode II — 512-FFT, 384 carriers, 4 kHz spacing.</summary>
        ModeII,
        /// <summary>Mode III — 256-FFT, 192 carriers, 8 kHz spacing.</summary>
        ModeIII,
        /// <summary>Mode IV — 1024-FFT, 768 carriers, 2 kHz spacing.</summary>
        ModeIV
    }

    /// <summary>
    /// Serializable settings for <see cref="TdmbPersonality"/> — a T-DMB / DAB COFDM signal. All modes
    /// keep a 2.048 MHz signal bandwidth. Modulation is DQPSK; this v1 approximates it with plain QPSK
    /// (no differential encoding). Representative v1 core.
    /// </summary>
    [DataContract]
    public sealed class TdmbConfig
    {
        /// <summary>DAB transmission mode.</summary>
        [DataMember] public DabMode Mode { get; set; } = DabMode.ModeI;

        /// <summary>Number of OFDM symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 16;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
