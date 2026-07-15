using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Sdmb
{
    /// <summary>
    /// An (approximate) Satellite-DMB personality (Signal Studio for S-DMB, E4438C-407 v1 core):
    /// generates a representative CDM (spread-spectrum) signal via the shared <see cref="DsssEngine"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ The S-DMB (System E) air-interface physical layer could not be confirmed from primary
    /// Keysight/standards literature during research, so this is a <b>representative CDM stimulus, not a
    /// verified S-DMB waveform</b>. Chip rate, spreading, FEC, and framing are placeholders to be
    /// validated. Treat the output as "a QPSK spread-spectrum signal," not as standards-compliant S-DMB.
    /// </remarks>
    public sealed class SdmbPersonality : IWaveformPersonality
    {
        private SdmbConfig _config = new SdmbConfig();

        /// <inheritdoc/>
        public string Id => "s-dmb";

        /// <inheritdoc/>
        public string DisplayName => "S-DMB (CDM, approx.)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is SdmbConfig sc))
                throw new ArgumentException("Expected an SdmbConfig.", nameof(cfg));
            _config = sc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            SdmbConfig cfg = _config ?? new SdmbConfig();
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
                Name = "S-DMB"
            }, progress);
        }
    }
}
