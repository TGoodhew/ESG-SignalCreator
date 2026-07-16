using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.GsmEdge
{
    /// <summary>GSM-family modulation: GSM/GPRS GMSK, or EDGE 3π/8-rotated 8-PSK.</summary>
    public enum GsmModulation
    {
        /// <summary>GMSK (GSM/GPRS) — continuous-phase, constant envelope.</summary>
        Gmsk = 0,
        /// <summary>EDGE — 3π/8-rotated 8-PSK (3 bits/symbol), pulse-shaped (non-constant envelope).</summary>
        Edge8Psk = 1
    }

    /// <summary>
    /// Serializable settings for <see cref="GsmEdgePersonality"/> — a GSM-family carrier. Implements the
    /// GSM/GPRS **GMSK** physical layer and (v2) **EDGE 3π/8-rotated 8-PSK**. Full burst/multiframe
    /// framing, transport-channel coding, and TSC/training sequences are deferred (see the N7602B doc).
    /// </summary>
    [DataContract]
    public sealed class GsmEdgeConfig
    {
        /// <summary>Modulation: GMSK (v1) or EDGE 3π/8-rotated 8-PSK (v2).</summary>
        [DataMember] public GsmModulation Modulation { get; set; } = GsmModulation.Gmsk;

        /// <summary>Symbol rate, in hertz. GSM/EDGE is 1625000/6 ≈ 270.833 ksym/s.</summary>
        [DataMember] public double SymbolRateHz { get; set; } = 1625000.0 / 6.0;

        /// <summary>Oversampling: I/Q samples generated per symbol. Sample rate = SymbolRate × this.</summary>
        [DataMember] public int SamplesPerSymbol { get; set; } = 16;

        /// <summary>Number of symbols (bits) to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 1250;

        /// <summary>Gaussian filter bandwidth-time product. GSM uses BT = 0.3.</summary>
        [DataMember] public double Bt { get; set; } = 0.3;

        /// <summary>Gaussian pulse span, in symbols (GMSK).</summary>
        [DataMember] public int GaussianSpanSymbols { get; set; } = 4;

        /// <summary>Root-raised-cosine roll-off for the EDGE 8-PSK pulse shaping (representative pulse).</summary>
        [DataMember] public double EdgeRrcBeta { get; set; } = 0.3;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
