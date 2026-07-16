using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Wlan
{
    /// <summary>
    /// An 802.11 WLAN personality (Signal Studio for 802.11 WLAN, N7617B v1 core): generates a generic
    /// OFDM signal with 802.11a/g/n numerology — 312.5 kHz subcarrier spacing, 64- or 128-point FFT with
    /// the standard used-subcarrier count and cyclic prefix — via the shared <see cref="OfdmEngine"/>.
    /// </summary>
    /// <remarks>
    /// Two modes: the default generic OFDM fill (v1 core), or — with <see cref="WlanConfig.FrameStructured"/>
    /// set (20 MHz) — a representative 802.11a/g <b>PPDU</b> (v2, #191) via <see cref="WlanPpdu"/> with an
    /// optional L-LTF preamble, pilot subcarriers (±7, ±21), and a selectable guard interval. The L-STF/
    /// L-SIG fields, channel coding/interleaving, MAC framing, MIMO, and 80/160 MHz remain deferred.
    /// </remarks>
    public sealed class WlanPersonality : IWaveformPersonality
    {
        private WlanConfig _config = new WlanConfig();

        /// <inheritdoc/>
        public string Id => "wlan-80211";

        /// <inheritdoc/>
        public string DisplayName => "802.11 WLAN (OFDM)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is WlanConfig wc))
                throw new ArgumentException("Expected a WlanConfig.", nameof(cfg));
            _config = wc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            WlanConfig cfg = _config ?? new WlanConfig();
            if (cfg.FrameStructured)
                return WlanPpdu.Generate(cfg, progress);

            Numerology(cfg.Bandwidth, out int fft, out int occupied, out int cp);
            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = fft,
                CyclicPrefix = cp,
                OccupiedCarriers = occupied,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = 312.5e3,
                Modulation = cfg.Modulation,
                Data = cfg.Data,
                Name = "802.11 WLAN"
            }, progress);
        }

        private static void Numerology(WlanBandwidth bw, out int fft, out int occupied, out int cp)
        {
            switch (bw)
            {
                case WlanBandwidth.Bw40MHz: fft = 128; occupied = 108; cp = 32; break;
                case WlanBandwidth.Bw20MHz:
                default: fft = 64; occupied = 52; cp = 16; break;
            }
        }
    }
}
