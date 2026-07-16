using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Tdmb
{
    /// <summary>
    /// A T-DMB personality (Signal Studio for T-DMB, N7616B v1 core): generates the DAB COFDM signal
    /// underlying Terrestrial-DMB (2.048 MHz bandwidth, mode-dependent FFT/guard) via the shared
    /// <see cref="OfdmEngine"/>. DQPSK modulation is approximated with plain QPSK.
    /// </summary>
    /// <remarks>
    /// Two modes: the default generic OFDM fill with plain QPSK (v1 core), or — with
    /// <see cref="TdmbConfig.FrameStructured"/> set — a DAB transmission frame (v2, #195) via
    /// <see cref="TdmbFrame"/>: a **null symbol** + **phase-reference symbol** (the synchronisation
    /// channel) followed by **differentially-encoded DQPSK** data symbols. FIC/MSC multiplex, the
    /// synchronisation TII, and convolutional coding remain deferred.
    /// </remarks>
    public sealed class TdmbPersonality : IWaveformPersonality
    {
        internal const double SignalBandwidthHz = 2.048e6;

        private TdmbConfig _config = new TdmbConfig();

        /// <inheritdoc/>
        public string Id => "t-dmb";

        /// <inheritdoc/>
        public string DisplayName => "T-DMB (DAB COFDM)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is TdmbConfig tc))
                throw new ArgumentException("Expected a TdmbConfig.", nameof(cfg));
            _config = tc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            TdmbConfig cfg = _config ?? new TdmbConfig();
            if (cfg.FrameStructured)
                return TdmbFrame.Generate(cfg, progress);

            Numerology(cfg.Mode, out int fft, out int occupied, out int cp);
            double spacing = SignalBandwidthHz / fft; // keeps a 2.048 MHz sample rate for all modes

            return OfdmEngine.Generate(new OfdmEngine.Params
            {
                FftSize = fft,
                CyclicPrefix = cp,
                OccupiedCarriers = occupied,
                SymbolCount = cfg.SymbolCount,
                SubcarrierSpacingHz = spacing,
                Modulation = Modulation.QPSK, // DQPSK approximated by QPSK
                Data = cfg.Data,
                Name = "T-DMB"
            }, progress);
        }

        /// <summary>Map a DAB mode to (FFT size, active carriers, guard-interval length).</summary>
        internal static void Numerology(DabMode mode, out int fft, out int occupied, out int cp)
        {
            switch (mode)
            {
                case DabMode.ModeII: fft = 512; occupied = 384; cp = 126; break;
                case DabMode.ModeIII: fft = 256; occupied = 192; cp = 63; break;
                case DabMode.ModeIV: fft = 1024; occupied = 768; cp = 252; break;
                case DabMode.ModeI:
                default: fft = 2048; occupied = 1536; cp = 504; break;
            }
        }
    }
}
