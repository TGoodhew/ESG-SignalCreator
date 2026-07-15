using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.WimaxFixed
{
    /// <summary>
    /// A fixed-WiMAX personality (Signal Studio for 802.16-2004, N7613A v1 core): generates the
    /// 256-FFT OFDM PHY (200 used subcarriers) via the shared <see cref="OfdmEngine"/>, with the
    /// sample rate derived from the channel bandwidth (8/7 sampling factor) and a selectable CP ratio.
    /// </summary>
    /// <remarks>
    /// Representative v1 core, not a standards-compliant burst: no long/short preamble, FCH, DL/UL-MAP,
    /// DCD/UCD, pilot patterns, or RS-CC channel coding. Those are deferred.
    /// </remarks>
    public sealed class WimaxFixedPersonality : IWaveformPersonality
    {
        private const int Fft = 256;
        private const int Used = 200; // 192 data + 8 pilots (DC nulled)

        private WimaxFixedConfig _config = new WimaxFixedConfig();

        /// <inheritdoc/>
        public string Id => "wimax-fixed";

        /// <inheritdoc/>
        public string DisplayName => "802.16-2004 WiMAX (OFDM)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is WimaxFixedConfig wc))
                throw new ArgumentException("Expected a WimaxFixedConfig.", nameof(cfg));
            _config = wc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            WimaxFixedConfig cfg = _config ?? new WimaxFixedConfig();
            if (cfg.ChannelBandwidthHz <= 0)
                throw new InvalidOperationException("ChannelBandwidthHz must be positive.");

            double sampleRate = cfg.ChannelBandwidthHz * 8.0 / 7.0; // WiMAX sampling factor
            double spacing = sampleRate / Fft;
            int cp = (int)(Fft * CpFraction(cfg.CyclicPrefixRatio));

            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = Fft,
                CyclicPrefix = cp,
                OccupiedCarriers = Used,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = spacing,
                Modulation = cfg.Modulation,
                Data = cfg.Data,
                Name = "802.16-2004 WiMAX"
            }, progress);
        }

        internal static double CpFraction(CpRatio r)
        {
            switch (r)
            {
                case CpRatio.OneQuarter: return 1.0 / 4.0;
                case CpRatio.OneSixteenth: return 1.0 / 16.0;
                case CpRatio.OneThirtySecond: return 1.0 / 32.0;
                case CpRatio.OneEighth:
                default: return 1.0 / 8.0;
            }
        }
    }
}
