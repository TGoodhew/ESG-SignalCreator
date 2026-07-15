using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Bluetooth
{
    /// <summary>
    /// Serializable settings for <see cref="BluetoothPersonality"/> — a GFSK-modulated Bluetooth
    /// carrier. This v1 implements the Basic Rate / LE GFSK physical layer (Gaussian-filtered FSK
    /// with a configurable modulation index); EDR (π/4-DQPSK / 8DPSK), LE coded PHY, packet framing,
    /// and hopping are deferred (see the N7606B requirements doc).
    /// </summary>
    [DataContract]
    public sealed class BluetoothConfig
    {
        /// <summary>Symbol rate, in hertz. Bluetooth BR / LE 1M = 1 Msym/s; LE 2M = 2 Msym/s.</summary>
        [DataMember] public double SymbolRateHz { get; set; } = 1e6;

        /// <summary>Oversampling: I/Q samples per symbol. Sample rate = SymbolRate × this.</summary>
        [DataMember] public int SamplesPerSymbol { get; set; } = 16;

        /// <summary>Number of symbols (bits) to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 1024;

        /// <summary>GFSK modulation index (Bluetooth BR nominal 0.32; range 0.28–0.35). LE uses ~0.5.</summary>
        [DataMember] public double ModulationIndex { get; set; } = 0.32;

        /// <summary>Gaussian filter bandwidth-time product. Bluetooth uses BT = 0.5.</summary>
        [DataMember] public double Bt { get; set; } = 0.5;

        /// <summary>Gaussian pulse span, in symbols.</summary>
        [DataMember] public int GaussianSpanSymbols { get; set; } = 3;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
