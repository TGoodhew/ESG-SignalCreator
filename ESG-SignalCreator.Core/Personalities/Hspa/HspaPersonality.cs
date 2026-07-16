using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Hspa
{
    /// <summary>
    /// A 3GPP W-CDMA HSPA personality (Signal Studio for 3GPP W-CDMA HSPA, E4438C-419 v1 core):
    /// generates a single-code HS-PDSCH-style signal — QPSK or 16QAM data spread by an SF-16 OVSF
    /// code, complex-scrambled, and RRC-shaped (β = 0.22) at 3.84 Mcps — via the shared
    /// <see cref="DsssEngine"/>. The defining HSPA feature captured here is higher-order modulation
    /// (16QAM) on the shared/high-speed channel.
    /// </summary>
    /// <remarks>
    /// Representative v1 core, not a standards-compliant HSDPA/HSUPA link (no HS-SCCH/HS-DPCCH,
    /// E-DCH channels, H-ARQ, TTI structure, or CQI/rate control — those are deferred).
    /// </remarks>
    public sealed class HspaPersonality : IWaveformPersonality
    {
        private HspaConfig _config = new HspaConfig();

        /// <inheritdoc/>
        public string Id => "wcdma-hspa";

        /// <inheritdoc/>
        public string DisplayName => "3GPP W-CDMA HSPA";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is HspaConfig hc))
                throw new ArgumentException("Expected an HspaConfig.", nameof(cfg));
            _config = hc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            HspaConfig cfg = _config ?? new HspaConfig();
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
                Name = "W-CDMA HSPA"
            }, progress);
        }
    }
}
