using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Wcdma
{
    /// <summary>
    /// A 3GPP W-CDMA (FDD) personality (Signal Studio for 3GPP W-CDMA FDD, N7600B v1 core): generates
    /// a single-code downlink-style signal — QPSK data spread by an OVSF code, complex-scrambled, and
    /// RRC-shaped (β = 0.22) at the 3.84 Mcps chip rate — via the shared <see cref="DsssEngine"/>.
    /// </summary>
    /// <remarks>
    /// Single-code by default; set <see cref="WcdmaConfig.CodeChannelCount"/> &gt; 1 to sum multiple
    /// orthogonal OVSF code channels into a **multi-code downlink composite** (v2, #183). Still not
    /// standards-compliant: no P-CCPCH/CPICH/SCH, slot/frame structure, TFCI, transmit diversity, or
    /// HSPA channels — those remain deferred.
    /// </remarks>
    public sealed class WcdmaPersonality : IWaveformPersonality
    {
        private WcdmaConfig _config = new WcdmaConfig();

        /// <inheritdoc/>
        public string Id => "wcdma-fdd";

        /// <inheritdoc/>
        public string DisplayName => "3GPP W-CDMA FDD";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is WcdmaConfig wc))
                throw new ArgumentException("Expected a WcdmaConfig.", nameof(cfg));
            _config = wc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            WcdmaConfig cfg = _config ?? new WcdmaConfig();
            if (cfg.CodeChannelCount > cfg.SpreadingFactor)
                throw new InvalidOperationException("CodeChannelCount must be <= SpreadingFactor.");

            DsssEngine.CodeChannel[] channels = null;
            if (cfg.CodeChannelCount > 1)
            {
                channels = new DsssEngine.CodeChannel[cfg.CodeChannelCount];
                for (int k = 0; k < channels.Length; k++)
                    channels[k] = new DsssEngine.CodeChannel
                    {
                        OvsfIndex = k,
                        PowerDb = 0.0,
                        Modulation = cfg.Modulation,
                        Data = cfg.Data
                    };
            }

            return DsssEngine.Generate(new DsssEngine.Params
            {
                ChipRateHz = cfg.ChipRateHz,
                SamplesPerChip = cfg.SamplesPerChip,
                SymbolCount = cfg.SymbolCount,
                SpreadingFactor = cfg.SpreadingFactor,
                OvsfIndex = cfg.OvsfIndex,
                Modulation = cfg.Modulation,
                RrcBeta = cfg.RrcBeta,
                Scramble = cfg.Scramble,
                ScrambleSeed = cfg.ScrambleSeed,
                Data = cfg.Data,
                CodeChannels = channels,
                Name = "W-CDMA FDD"
            }, progress);
        }
    }
}
