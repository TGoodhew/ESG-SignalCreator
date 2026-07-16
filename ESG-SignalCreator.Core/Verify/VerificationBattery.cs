using System;
using System.Collections.Generic;
using System.IO;
using EsgSignalCreator.Export;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.BroadcastRadio;
using EsgSignalCreator.Personalities.Bluetooth;
using EsgSignalCreator.Personalities.Cdma2000;
using EsgSignalCreator.Personalities.CustomIq;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.DigitalVideo;
using EsgSignalCreator.Personalities.GsmEdge;
using EsgSignalCreator.Personalities.Hspa;
using EsgSignalCreator.Personalities.Jitter;
using EsgSignalCreator.Personalities.Lte;
using EsgSignalCreator.Personalities.MultiCarrier;
using EsgSignalCreator.Personalities.Multitone;
using EsgSignalCreator.Personalities.MultitoneDistortion;
using EsgSignalCreator.Personalities.Pulse;
using EsgSignalCreator.Personalities.TdScdma;
using EsgSignalCreator.Personalities.Tdmb;
using EsgSignalCreator.Personalities.Wcdma;
using EsgSignalCreator.Personalities.WimaxFixed;   // also provides CpRatio (reused by WiMAX-Mobile / DVB-T)
using EsgSignalCreator.Personalities.WimaxMobile;
using EsgSignalCreator.Personalities.Wlan;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// One personality in the closed-loop verification battery: its id, a short description, the
    /// analyzer measurement plan (span + which checks apply), and a builder that generates a short
    /// representative waveform via the real Core personality.
    /// </summary>
    public sealed class BatteryEntry
    {
        public string Id;
        public string Description;
        public double SpanHz;
        public double AcpCarrierBwHz; // ACP carrier integration bandwidth (0 = ACP not applicable)
        public bool CheckTone;        // verify a discrete tone lands at carrier + offset
        public bool CheckAcp;         // measure adjacent-channel power
        public Func<IProgress<int>, WaveformModel> Build;
    }

    /// <summary>
    /// The canonical closed-loop verification battery (#227): one representative waveform per source
    /// personality, exercising each personality's v2 mode where that is the headline capability
    /// (frame-structured OFDM, multi-code CDMA, EDGE-8PSK, Bluetooth EDR, RDS). This is the single
    /// source of truth the HIL harness enumerates, so a new personality is a one-line addition here
    /// rather than a silent gap. <see cref="PersonalityIds"/> is frozen and guarded by a test that
    /// fails loudly if the battery and the personality catalogue drift apart.
    /// </summary>
    /// <remarks>
    /// Waveforms are kept short (small symbol/subframe/length counts) so the whole battery builds
    /// quickly offline. Spans and ACP bandwidths are representative starting points for the bench run
    /// (#229) and may be tuned there.
    /// </remarks>
    public static class VerificationBattery
    {
        /// <summary>
        /// The personalities covered by the battery, in run order. Mirrors the app's
        /// PersonalityRegistry; keep the two in step (guarded by VerificationBatteryTests).
        /// </summary>
        public static readonly string[] PersonalityIds =
        {
            "cw", "multitone", "multitone-distortion", "multi-carrier", "custom-mod", "pulse", "jitter",
            "gsm-edge", "bluetooth", "wcdma-fdd", "wcdma-hspa", "cdma2000", "td-scdma", "lte-fdd", "lte-tdd",
            "wlan-80211", "wimax-fixed", "wimax-mobile", "t-dmb", "digital-video", "broadcast-radio", "awgn",
            "import-iq",
        };

        /// <summary>Build every battery entry. <paramref name="offsetHz"/> sets the CW/import-IQ tone offset.</summary>
        public static IReadOnlyList<BatteryEntry> All(double offsetHz)
        {
            var list = new List<BatteryEntry>();

            list.Add(new BatteryEntry
            {
                Id = "cw", Description = "Unmodulated tone at carrier + offset",
                SpanHz = ToneSpan(offsetHz), CheckTone = true,
                Build = p => Gen(new CwPersonality(), new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "multitone", Description = "4-tone Newman multitone, 1 MHz spacing",
                SpanHz = 10e6,
                Build = p => Gen(new MultitonePersonality(), new MultitoneConfig
                {
                    SampleRateHz = 10e6, Length = 16384, Phase = PhaseStrategy.Newman,
                    Tones = MultitonePersonality.AutoSpacing(4, 1e6, 0, 0),
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "multitone-distortion", Description = "16-tone comb with an NPR notch",
                SpanHz = 5e6,
                Build = p => Gen(new MultitoneDistortionPersonality(), new MultitoneDistortionConfig
                {
                    SampleRateHz = 40e6, Length = 8192, ToneCount = 16, ToneSpacingHz = 100e3,
                    NotchEnabled = true, NotchWidthHz = 1e6,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "multi-carrier", Description = "3-carrier composite, 1 MHz spacing",
                SpanHz = 10e6,
                Build = p => Gen(new MultiCarrierPersonality(), new MultiCarrierConfig
                {
                    SampleRateHz = 10e6, Length = 16384, Carriers = MultiCarrierPersonality.EvenlySpaced(3, 1e6, 0),
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "custom-mod", Description = "QAM16, 1 Msym/s, RRC α=0.35",
                SpanHz = 5e6, AcpCarrierBwHz = 1.35e6, CheckAcp = true,
                Build = p => Gen(new CustomModPersonality(), new CustomModConfig
                {
                    Modulation = Modulation.QAM16, SymbolRateHz = 1e6, SamplesPerSymbol = 8, Alpha = 0.35, SymbolCount = 1024,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "pulse", Description = "1 µs LFM chirp, 10 µs PRI",
                SpanHz = 20e6,
                Build = p => Gen(new PulsePersonality(), new PulseConfig
                {
                    SampleRateHz = 50e6, Length = 8192, PulseWidthSec = 1e-6, PriSec = 10e-6, RiseFallSec = 0.0,
                    Modulation = IntraPulseModulation.LinearFmChirp, ChirpBandwidthHz = 5e6,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "jitter", Description = "10 MHz clock, sinusoidal SJ 0.2 UIpp",
                SpanHz = 20e6,
                Build = p => Gen(new JitterPersonality(), new JitterConfig
                {
                    SampleRateHz = 100e6, Length = 8192, ClockRateHz = 10e6,
                    PeriodicShape = JitterShape.Sinusoidal, PeriodicRateHz = 100e3, PeriodicUiPp = 0.2,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "gsm-edge", Description = "EDGE 3π/8 8-PSK (v2)",
                SpanHz = 2e6, AcpCarrierBwHz = 200e3, CheckAcp = true,
                Build = p => Gen(new GsmEdgePersonality(), new GsmEdgeConfig
                {
                    Modulation = GsmModulation.Edge8Psk, SymbolCount = 200, SamplesPerSymbol = 16,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "bluetooth", Description = "EDR 8-DPSK 3 Mb/s (v2)",
                SpanHz = 5e6, AcpCarrierBwHz = 1e6, CheckAcp = true,
                Build = p => Gen(new BluetoothPersonality(), new BluetoothConfig
                {
                    Modulation = BluetoothModulation.Edr3Mbps, SymbolCount = 256, SamplesPerSymbol = 16,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "wcdma-fdd", Description = "W-CDMA 4-code multiplex (v2)",
                SpanHz = 15e6, AcpCarrierBwHz = 3.84e6, CheckAcp = true,
                Build = p => Gen(new WcdmaPersonality(), new WcdmaConfig
                {
                    SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "wcdma-hspa", Description = "HSPA QAM16 4-code multiplex (v2)",
                SpanHz = 15e6, AcpCarrierBwHz = 3.84e6, CheckAcp = true,
                Build = p => Gen(new HspaPersonality(), new HspaConfig
                {
                    SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "cdma2000", Description = "cdma2000 4-code multiplex (v2)",
                SpanHz = 6e6, AcpCarrierBwHz = 1.2288e6, CheckAcp = true,
                Build = p => Gen(new Cdma2000Personality(), new Cdma2000Config
                {
                    SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "td-scdma", Description = "TD-SCDMA 4-code multiplex (v2)",
                SpanHz = 6e6, AcpCarrierBwHz = 1.28e6, CheckAcp = true,
                Build = p => Gen(new TdScdmaPersonality(), new TdScdmaConfig
                {
                    SymbolCount = 64, SpreadingFactor = 16, CodeChannelCount = 4,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "lte-fdd", Description = "LTE FDD 5 MHz E-UTRA frame (v2)",
                SpanHz = 15e6, AcpCarrierBwHz = 4.5e6, CheckAcp = true,
                Build = p => Gen(new LteFddPersonality(), new LteConfig
                {
                    Bandwidth = LteBandwidth.Bw5MHz, Modulation = Modulation.QAM16, FrameStructured = true,
                    PhysicalCellId = 0, SubframeCount = 1, CyclicPrefix = LteCyclicPrefix.Normal,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "lte-tdd", Description = "LTE TDD 5 MHz frame, config 1 (v2)",
                SpanHz = 15e6, AcpCarrierBwHz = 4.5e6, CheckAcp = true,
                Build = p => Gen(new LteTddPersonality(), new LteConfig
                {
                    Bandwidth = LteBandwidth.Bw5MHz, Modulation = Modulation.QAM16, FrameStructured = true,
                    PhysicalCellId = 0, SubframeCount = 10, CyclicPrefix = LteCyclicPrefix.Normal,
                    TddUlDlConfig = 1, TddSpecialSubframeConfig = 7,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "wlan-80211", Description = "802.11a/g 20 MHz PPDU (v2)",
                SpanHz = 40e6, AcpCarrierBwHz = 16.6e6, CheckAcp = true,
                Build = p => Gen(new WlanPersonality(), new WlanConfig
                {
                    Bandwidth = WlanBandwidth.Bw20MHz, SymbolCount = 4, Modulation = Modulation.QAM16,
                    FrameStructured = true, GuardInterval = WlanGuardInterval.Long, IncludeLtfPreamble = true,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "wimax-fixed", Description = "802.16-2004 3.5 MHz frame (v2)",
                SpanHz = 10e6, AcpCarrierBwHz = 3e6, CheckAcp = true,
                Build = p => Gen(new WimaxFixedPersonality(), new WimaxFixedConfig
                {
                    ChannelBandwidthHz = 3.5e6, CyclicPrefixRatio = CpRatio.OneEighth, SymbolCount = 8,
                    Modulation = Modulation.QAM16, FrameStructured = true, IncludePreamble = true,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "wimax-mobile", Description = "802.16e 512-FFT OFDMA frame (v2)",
                SpanHz = 15e6, AcpCarrierBwHz = 5e6, CheckAcp = true,
                Build = p => Gen(new WimaxMobilePersonality(), new WimaxMobileConfig
                {
                    FftSize = WimaxFftSize.Fft512, CyclicPrefixRatio = CpRatio.OneEighth, SymbolCount = 8,
                    Modulation = Modulation.QAM16, FrameStructured = true, IncludePreamble = true,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "t-dmb", Description = "T-DMB Mode III COFDM frame (v2)",
                SpanHz = 5e6, AcpCarrierBwHz = 1.536e6, CheckAcp = true,
                Build = p => Gen(new TdmbPersonality(), new TdmbConfig
                {
                    Mode = DabMode.ModeIII, SymbolCount = 16, FrameStructured = true,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "digital-video", Description = "DVB-T 2K COFDM with scattered pilots (v2)",
                SpanHz = 20e6, AcpCarrierBwHz = 7.6e6, CheckAcp = true,
                Build = p => Gen(new DigitalVideoPersonality(), new DigitalVideoConfig
                {
                    Mode = DvbtMode.Mode2K, GuardInterval = CpRatio.OneEighth, SymbolCount = 8,
                    Modulation = Modulation.QPSK, FrameStructured = true,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "broadcast-radio", Description = "Stereo FM with RDS subcarrier (v2)",
                SpanHz = 1e6,
                Build = p => Gen(new BroadcastRadioPersonality(), new BroadcastRadioConfig
                {
                    SampleRateHz = 400e3, Length = 8000, Stereo = true, Rds = true, RdsDeviationHz = 4e3,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "awgn", Description = "Band-limited AWGN, 2 MHz, 10 dB crest",
                SpanHz = 5e6,
                Build = p => Gen(new AwgnPersonality(), new AwgnConfig
                {
                    SampleRateHz = 10e6, Length = 32768, NoiseBandwidthHz = 2e6, CrestFactorDb = 10,
                }, p),
            });

            list.Add(new BatteryEntry
            {
                Id = "import-iq", Description = "CW round-tripped through an I/Q CSV import",
                SpanHz = ToneSpan(offsetHz), CheckTone = true,
                Build = p =>
                {
                    var src = new CwPersonality();
                    src.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz });
                    string path = Path.Combine(Path.GetTempPath(), "battery-import-iq.csv");
                    WaveformExporter.SaveCsv(path, src.Calculate(null));
                    return Gen(new ImportIqPersonality(), new ImportIqConfig { Path = path, SampleRateHz = 10e6 }, p);
                },
            });

            return list;
        }

        /// <summary>The battery entry for one personality id, or null if the id is not in the battery.</summary>
        public static BatteryEntry Get(string id, double offsetHz)
        {
            foreach (BatteryEntry e in All(offsetHz))
                if (string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        private static double ToneSpan(double offsetHz) => Math.Max(1e6, 4 * Math.Abs(offsetHz) + 1e6);

        private static WaveformModel Gen<TConfig>(IWaveformPersonality personality, TConfig config, IProgress<int> progress)
        {
            personality.LoadConfig(config);
            return personality.Calculate(progress);
        }
    }
}
