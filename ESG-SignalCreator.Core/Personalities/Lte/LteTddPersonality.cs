using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Lte
{
    /// <summary>
    /// A 3GPP LTE TDD personality (Signal Studio for 3GPP LTE TDD, N7625B v1 core): generates the same
    /// downlink OFDM signal as <see cref="LteFddPersonality"/> (shared <see cref="LteConfig"/> and OFDM
    /// mapping) — the physical-layer OFDM numerology is identical for FDD and TDD.
    /// </summary>
    /// <remarks>
    /// The TDD-specific parts — DL/UL subframe configurations, the special subframe
    /// (DwPTS/GP/UpPTS), and the 10 ms frame structure — are deferred, as are the reference/sync
    /// signals and channel mapping (same deferrals as the FDD personality).
    /// </remarks>
    public sealed class LteTddPersonality : IWaveformPersonality
    {
        private LteConfig _config = new LteConfig();

        /// <inheritdoc/>
        public string Id => "lte-tdd";

        /// <inheritdoc/>
        public string DisplayName => "3GPP LTE TDD";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is LteConfig lc))
                throw new ArgumentException("Expected an LteConfig.", nameof(cfg));
            _config = lc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress) =>
            LteWaveform.Generate(_config ?? new LteConfig(), "LTE TDD", progress);
    }
}
