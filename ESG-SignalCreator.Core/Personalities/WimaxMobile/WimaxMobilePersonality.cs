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
    /// Representative v1 core, not a standards-compliant 802.16e frame: no OFDMA subchannel permutation
    /// zones (PUSC/FUSC/AMC), preamble, FCH/DL-MAP/UL-MAP, pilots, MIMO (Matrix A/B), or CTC/CC coding.
    /// Those are deferred.
    /// </remarks>
    public sealed class WimaxMobilePersonality : IWaveformPersonality
    {
        private const double SubcarrierSpacingHz = 10.9375e3;

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
        private static void Numerology(WimaxFftSize size, out int fft, out int occupied)
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
