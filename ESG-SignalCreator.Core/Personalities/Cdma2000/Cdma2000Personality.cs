using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Cdma2000
{
    /// <summary>
    /// A 3GPP2 CDMA personality (Signal Studio for 3GPP2 CDMA, N7601B v1 core): generates a single-code
    /// cdma2000 forward-link-style signal — QPSK data spread by a Walsh (OVSF) code, PN-scrambled, and
    /// pulse-shaped at the 1.2288 Mcps chip rate — via the shared <see cref="DsssEngine"/>.
    /// </summary>
    /// <remarks>
    /// Representative v1 core, not a standards-compliant cdma2000/1xEV-DO link (no pilot/sync/paging
    /// channels, radio configurations, frame/PCG structure, or the exact cdma2000 baseband filter —
    /// an RRC approximation is used). Those are deferred.
    /// </remarks>
    public sealed class Cdma2000Personality : IWaveformPersonality
    {
        private Cdma2000Config _config = new Cdma2000Config();

        /// <inheritdoc/>
        public string Id => "cdma2000";

        /// <inheritdoc/>
        public string DisplayName => "3GPP2 CDMA (cdma2000)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is Cdma2000Config cc))
                throw new ArgumentException("Expected a Cdma2000Config.", nameof(cfg));
            _config = cc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            Cdma2000Config cfg = _config ?? new Cdma2000Config();
            if (cfg.CodeChannelCount > cfg.SpreadingFactor)
                throw new InvalidOperationException("CodeChannelCount must be <= SpreadingFactor.");
            DsssEngine.CodeChannel[] channels = null;
            if (cfg.CodeChannelCount > 1)
            {
                channels = new DsssEngine.CodeChannel[cfg.CodeChannelCount];
                for (int k = 0; k < channels.Length; k++)
                    channels[k] = new DsssEngine.CodeChannel { OvsfIndex = k, PowerDb = 0.0, Modulation = cfg.Modulation, Data = cfg.Data };
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
                Name = "cdma2000"
            }, progress);
        }
    }
}
