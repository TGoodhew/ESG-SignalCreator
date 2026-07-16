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
    /// Two modes: the default generic OFDM fill (v1 core), or — with <see cref="LteConfig.FrameStructured"/>
    /// set — a proper E-UTRA TDD downlink frame (v2, #189) built by <see cref="LteFrame"/>: the D/S/U
    /// subframe pattern of the selected uplink-downlink configuration, the special subframe (DwPTS
    /// transmits downlink; GP/UpPTS are silent), TDD-positioned PSS/SSS, CRS, and a PDSCH data fill.
    /// Uplink physical channels, MIMO, HARQ, and carrier aggregation remain deferred.
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
        public WaveformModel Calculate(IProgress<int> progress)
        {
            LteConfig cfg = _config ?? new LteConfig();
            return cfg.FrameStructured
                ? LteFrame.Generate(cfg, "LTE TDD", progress, tdd: true)
                : LteWaveform.Generate(cfg, "LTE TDD", progress);
        }
    }
}
