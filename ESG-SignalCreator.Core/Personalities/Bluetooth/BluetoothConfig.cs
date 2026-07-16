using System.Runtime.Serialization;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Bluetooth
{
    /// <summary>Bluetooth modulation: Basic-Rate/LE GFSK, or EDR π/4-DQPSK (2 Mbps) / 8-DPSK (3 Mbps).</summary>
    public enum BluetoothModulation
    {
        /// <summary>GFSK (BR / LE) — Gaussian-filtered FSK, constant envelope.</summary>
        Gfsk = 0,
        /// <summary>EDR 2 Mbps — differential π/4-DQPSK (2 bits/symbol), pulse-shaped.</summary>
        Edr2Mbps = 1,
        /// <summary>EDR 3 Mbps — differential 8-DPSK (3 bits/symbol), pulse-shaped.</summary>
        Edr3Mbps = 2
    }

    /// <summary>
    /// Serializable settings for <see cref="BluetoothPersonality"/> — a Bluetooth carrier. Implements
    /// the Basic-Rate / LE **GFSK** physical layer and (v2) **EDR** π/4-DQPSK / 8-DPSK. LE coded PHY,
    /// packet framing, and hopping are deferred (see the N7606B requirements doc).
    /// </summary>
    [DataContract]
    public sealed class BluetoothConfig
    {
        /// <summary>Modulation: GFSK (v1), or EDR π/4-DQPSK (2 Mbps) / 8-DPSK (3 Mbps) (v2). EDR keeps the
        /// 1 Msym/s symbol rate.</summary>
        [DataMember] public BluetoothModulation Modulation { get; set; } = BluetoothModulation.Gfsk;

        /// <summary>Symbol rate, in hertz. Bluetooth BR / LE 1M / EDR = 1 Msym/s; LE 2M = 2 Msym/s.</summary>
        [DataMember] public double SymbolRateHz { get; set; } = 1e6;

        /// <summary>Oversampling: I/Q samples per symbol. Sample rate = SymbolRate × this.</summary>
        [DataMember] public int SamplesPerSymbol { get; set; } = 16;

        /// <summary>Number of symbols (bits) to generate.</summary>
        [DataMember] public int SymbolCount { get; set; } = 1024;

        /// <summary>GFSK modulation index (Bluetooth BR nominal 0.32; range 0.28–0.35). LE uses ~0.5.</summary>
        [DataMember] public double ModulationIndex { get; set; } = 0.32;

        /// <summary>Gaussian filter bandwidth-time product. Bluetooth uses BT = 0.5.</summary>
        [DataMember] public double Bt { get; set; } = 0.5;

        /// <summary>Gaussian pulse span, in symbols (GFSK).</summary>
        [DataMember] public int GaussianSpanSymbols { get; set; } = 3;

        /// <summary>Root-raised-cosine roll-off for the EDR pulse shaping. Bluetooth EDR uses 0.4.</summary>
        [DataMember] public double EdrRrcBeta { get; set; } = 0.4;

        /// <summary>Payload bit source.</summary>
        [DataMember] public DataSource Data { get; set; } = DataSource.PN9;
    }
}
