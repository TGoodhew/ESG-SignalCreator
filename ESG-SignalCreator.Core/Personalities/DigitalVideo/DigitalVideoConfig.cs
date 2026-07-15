using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.DigitalVideo
{
    /// <summary>DVB-T COFDM transmission mode (FFT size).</summary>
    public enum DvbtMode
    {
        /// <summary>2K mode — 2048-point FFT.</summary>
        Mode2K,
        /// <summary>8K mode — 8192-point FFT.</summary>
        Mode8K
    }

    /// <summary>
    /// Serializable settings for <see cref="DigitalVideoPersonality"/> — a DVB-T COFDM signal for an
    /// 8 MHz channel (elementary rate 64/7 MHz). A representative v1 core covering the DVB-T OFDM PHY;
    /// other digital-video standards (ISDB-T, ATSC 8VSB, DVB-C/S QAM, DTMB) are deferred.
    /// </summary>
    [DataContract]
    public sealed class DigitalVideoConfig
    {
        /// <summary>DVB-T transmission mode (FFT size).</summary>
        [DataMember] public DvbtMode Mode { get; set; } = DvbtMode.Mode8K;

        /// <summary>Guard-interval ratio.</summary>
        [DataMember] public CpRatio GuardInterval { get; set; } = CpRatio.OneEighth;

        /// <summary>Number of OFDM symbols to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 8;

        /// <summary>Subcarrier modulation (QPSK / 16QAM / 64QAM).</summary>
        [DataMember] public Modulation Modulation { get; set; } = Modulation.QAM64;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
