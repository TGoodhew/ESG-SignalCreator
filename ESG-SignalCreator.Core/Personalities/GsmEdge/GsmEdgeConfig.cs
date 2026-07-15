using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.GsmEdge
{
    /// <summary>
    /// Serializable settings for <see cref="GsmEdgePersonality"/> — a GMSK-modulated GSM-family
    /// carrier. This v1 implements the GSM/GPRS GMSK physical layer (the defining modulation);
    /// EDGE 8PSK (3π/8-rotated), full burst/multiframe framing, and TSC/training sequences are
    /// deferred follow-ups (see the N7602B requirements doc).
    /// </summary>
    [DataContract]
    public sealed class GsmEdgeConfig
    {
        /// <summary>Symbol (bit) rate, in hertz. GSM is 1625000/6 ≈ 270.833 ksym/s.</summary>
        [DataMember] public double SymbolRateHz { get; set; } = 1625000.0 / 6.0;

        /// <summary>Oversampling: I/Q samples generated per symbol. Sample rate = SymbolRate × this.</summary>
        [DataMember] public int SamplesPerSymbol { get; set; } = 16;

        /// <summary>Number of symbols (bits) to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 1250;

        /// <summary>Gaussian filter bandwidth-time product. GSM uses BT = 0.3.</summary>
        [DataMember] public double Bt { get; set; } = 0.3;

        /// <summary>Gaussian pulse span, in symbols.</summary>
        [DataMember] public int GaussianSpanSymbols { get; set; } = 4;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
