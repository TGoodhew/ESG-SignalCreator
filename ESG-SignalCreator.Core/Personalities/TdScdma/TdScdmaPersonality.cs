using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.TdScdma
{
    /// <summary>
    /// A TD-SCDMA personality (Signal Studio for TD-SCDMA, N7612B v1 core): generates a single-code
    /// 1.28 Mcps low-chip-rate signal — QPSK/16QAM/64QAM data spread by an OVSF code, scrambled, and
    /// RRC-shaped (β = 0.22) — via the shared <see cref="DsssEngine"/>.
    /// </summary>
    /// <remarks>
    /// Representative v1 core, not a standards-compliant TD-SCDMA link (no 5 ms sub-frame / 7-timeslot
    /// TDD burst structure, DwPTS/UpPTS/GP, midamble codes, switching points, or HSDPA channels).
    /// Those are deferred.
    /// </remarks>
    public sealed class TdScdmaPersonality : IWaveformPersonality
    {
        private TdScdmaConfig _config = new TdScdmaConfig();

        /// <inheritdoc/>
        public string Id => "td-scdma";

        /// <inheritdoc/>
        public string DisplayName => "TD-SCDMA";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is TdScdmaConfig tc))
                throw new ArgumentException("Expected a TdScdmaConfig.", nameof(cfg));
            _config = tc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            TdScdmaConfig cfg = _config ?? new TdScdmaConfig();
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
                Name = "TD-SCDMA"
            }, progress);
        }
    }
}
