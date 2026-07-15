using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.WimaxFixed;

namespace EsgSignalCreator.Personalities.DigitalVideo
{
    /// <summary>
    /// A digital-video personality (Signal Studio for Digital Video, N7623B v1 core): generates the
    /// DVB-T COFDM PHY for an 8 MHz channel (elementary rate 64/7 MHz, 2K or 8K FFT) via the shared
    /// <see cref="OfdmEngine"/>.
    /// </summary>
    /// <remarks>
    /// Representative v1 core covering the DVB-T OFDM waveform. Not a standards-compliant transport:
    /// no TPS carriers, scattered/continual pilots, PRBS energy dispersal, RS/convolutional coding, or
    /// MPEG-TS framing — and other digital-video standards (ISDB-T, ATSC 8VSB, DVB-C/S QAM, DTMB) are
    /// not implemented. Those are deferred.
    /// </remarks>
    public sealed class DigitalVideoPersonality : IWaveformPersonality
    {
        // DVB-T elementary sampling rate for an 8 MHz channel.
        private const double ElementaryRateHz = 64e6 / 7.0;

        private DigitalVideoConfig _config = new DigitalVideoConfig();

        /// <inheritdoc/>
        public string Id => "digital-video";

        /// <inheritdoc/>
        public string DisplayName => "Digital Video (DVB-T COFDM)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is DigitalVideoConfig dc))
                throw new ArgumentException("Expected a DigitalVideoConfig.", nameof(cfg));
            _config = dc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            DigitalVideoConfig cfg = _config ?? new DigitalVideoConfig();
            Numerology(cfg.Mode, out int fft, out int occupied);
            int cp = (int)(fft * WimaxFixedPersonality.CpFraction(cfg.GuardInterval));
            double spacing = ElementaryRateHz / fft;

            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = fft,
                CyclicPrefix = cp,
                OccupiedCarriers = occupied,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = spacing,
                Modulation = cfg.Modulation,
                Data = cfg.Data,
                Name = "DVB-T"
            }, progress);
        }

        /// <summary>
        /// Map the DVB-T mode to (FFT, active carriers). DVB-T uses 1705 (2K) / 6817 (8K) carriers;
        /// this engine nulls DC and needs an even count, so 1704 / 6816 are used (representative).
        /// </summary>
        private static void Numerology(DvbtMode mode, out int fft, out int occupied)
        {
            switch (mode)
            {
                case DvbtMode.Mode2K: fft = 2048; occupied = 1704; break;
                case DvbtMode.Mode8K:
                default: fft = 8192; occupied = 6816; break;
            }
        }
    }
}
