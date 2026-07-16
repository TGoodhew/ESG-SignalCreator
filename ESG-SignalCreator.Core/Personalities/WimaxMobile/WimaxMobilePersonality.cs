using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.WimaxMobile
{
    /// <summary>
    /// A mobile-WiMAX personality (Signal Studio for 802.16 WiMAX, N7615B v1 core): generates a
    /// scalable-OFDMA-numerology signal (FFT 128/512/1024/2048 at the fixed 10.9375 kHz subcarrier
    /// spacing) via the shared <see cref="OfdmEngine"/>. Modelled as plain OFDM.
    /// </summary>
    /// <remarks>
    /// Two modes: the default generic OFDM fill (v1 core), or — with <see cref="WimaxMobileConfig.FrameStructured"/>
    /// set — a DL-OFDMA-style frame (v2, #193) via <see cref="WimaxMobileFrame"/> with an optional
    /// preamble symbol and a DL-PUSC pilot pattern. The exact PUSC/FUSC/AMC permutation zones,
    /// FCH/DL-MAP/UL-MAP, MIMO (Matrix A/B), and CTC/CC coding remain deferred.
    /// </remarks>
    public sealed class WimaxMobilePersonality : IWaveformPersonality
    {
        internal const double SubcarrierSpacingHz = 10.9375e3;

        private WimaxMobileConfig _config = new WimaxMobileConfig();

        /// <inheritdoc/>
        public string Id => "wimax-mobile";

        /// <inheritdoc/>
        public string DisplayName => "802.16e Mobile WiMAX (OFDMA)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is WimaxMobileConfig wc))
                throw new ArgumentException("Expected a WimaxMobileConfig.", nameof(cfg));
            _config = wc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            WimaxMobileConfig cfg = _config ?? new WimaxMobileConfig();
            if (cfg.FrameStructured)
                return WimaxMobileFrame.Generate(cfg, progress);

            Numerology(cfg.FftSize, out int fft, out int occupied);
            int cp = (int)(fft * WimaxFixedPersonality.CpFraction(cfg.CyclicPrefixRatio));

            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = fft,
                CyclicPrefix = cp,
                OccupiedCarriers = occupied,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = SubcarrierSpacingHz,
                Modulation = cfg.Modulation,
                Data = cfg.Data,
                Name = "802.16e Mobile WiMAX"
            }, progress);
        }

        /// <summary>Map the scalable FFT size to (FFT, used subcarriers) — ~82% occupancy, even.</summary>
        internal static void Numerology(WimaxFftSize size, out int fft, out int occupied)
        {
            switch (size)
            {
                case WimaxFftSize.Fft128: fft = 128; occupied = 84; break;
                case WimaxFftSize.Fft512: fft = 512; occupied = 420; break;
                case WimaxFftSize.Fft2048: fft = 2048; occupied = 1680; break;
                case WimaxFftSize.Fft1024:
                default: fft = 1024; occupied = 840; break;
            }
        }
    }
}
