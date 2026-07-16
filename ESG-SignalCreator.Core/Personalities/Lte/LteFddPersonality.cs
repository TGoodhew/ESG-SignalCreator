using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Lte
{
    /// <summary>
    /// A 3GPP LTE (FDD) personality (Signal Studio for 3GPP LTE, N7624B v1 core): generates a generic
    /// downlink OFDM signal with LTE numerology — 15 kHz subcarrier spacing and the standard FFT size /
    /// occupied-subcarrier count for the selected channel bandwidth — via the shared
    /// <see cref="OfdmEngine"/>.
    /// </summary>
    /// <remarks>
    /// Two modes: the default generic OFDM fill (v1 core), or — with <see cref="LteConfig.FrameStructured"/>
    /// set — a proper E-UTRA downlink radio-frame (v2, #188) with a 10 ms frame / 0.5 ms slots /
    /// per-symbol CP (normal or extended), correctly-positioned PSS/SSS and CRS (antenna port 0), and a
    /// PDSCH data fill (see <see cref="LteFrame"/>). Uplink, MIMO, HARQ, and carrier aggregation remain
    /// deferred. The generic path is shared with <see cref="LteTddPersonality"/>.
    /// </remarks>
    public sealed class LteFddPersonality : IWaveformPersonality
    {
        private LteConfig _config = new LteConfig();

        /// <inheritdoc/>
        public string Id => "lte-fdd";

        /// <inheritdoc/>
        public string DisplayName => "3GPP LTE FDD";

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
                ? LteFrame.Generate(cfg, "LTE FDD", progress)
                : LteWaveform.Generate(cfg, "LTE FDD", progress);
        }
    }

    /// <summary>Shared LTE OFDM parameter mapping used by both the FDD and TDD personalities.</summary>
    internal static class LteWaveform
    {
        public static WaveformModel Generate(LteConfig cfg, string name, IProgress<int> progress)
        {
            LteNumerology(cfg.Bandwidth, out int fft, out int occupied, out int cp);
            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = fft,
                CyclicPrefix = cp,
                OccupiedCarriers = occupied,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = 15e3,
                Modulation = cfg.Modulation,
                Data = cfg.Data,
                Name = name
            }, progress);
        }

        /// <summary>
        /// Map an LTE channel bandwidth to (FFT size, occupied subcarriers = RB×12, normal-CP length).
        /// The 15 MHz case uses a 2048-point FFT (rather than the non-power-of-two 1536) for the IFFT.
        /// </summary>
        public static void LteNumerology(LteBandwidth bw, out int fft, out int occupied, out int cp)
        {
            switch (bw)
            {
                case LteBandwidth.Bw1_4MHz: fft = 128; occupied = 72; cp = 9; break;    // 6 RB
                case LteBandwidth.Bw3MHz: fft = 256; occupied = 180; cp = 18; break;    // 15 RB
                case LteBandwidth.Bw5MHz: fft = 512; occupied = 300; cp = 36; break;    // 25 RB
                case LteBandwidth.Bw10MHz: fft = 1024; occupied = 600; cp = 72; break;  // 50 RB
                case LteBandwidth.Bw15MHz: fft = 2048; occupied = 900; cp = 144; break; // 75 RB (2048 FFT)
                case LteBandwidth.Bw20MHz:
                default: fft = 2048; occupied = 1200; cp = 144; break;                  // 100 RB
            }
        }
    }
}
