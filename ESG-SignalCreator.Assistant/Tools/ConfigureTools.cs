using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// The v1 configure tools (#82, §4.2): choose/configure the source personality, pick plot views,
    /// manage the project, and run Calculate. All are <see cref="ToolEffect.Configure"/> — they change
    /// project/app state only, never the instrument. They delegate to <see cref="IAssistantConfigureHost"/>;
    /// argument shape/validation is enforced by each tool's schema + the dispatcher.
    /// </summary>
    public static class ConfigureTools
    {
        public static IEnumerable<IAppTool> All() => new IAppTool[]
        {
            new SetSourcePersonalityTool(),
            new ConfigureCwTool(),
            new ConfigureMultitoneTool(),
            new ConfigureMultitoneDistortionTool(),
            new ConfigureCustomModulationTool(),
            new ConfigurePulseTool(),
            new ConfigureJitterTool(),
            new ConfigureGsmEdgeTool(),
            new ConfigureBluetoothTool(),
            new ConfigureWcdmaTool(),
            new ConfigureHspaTool(),
            new ConfigureCdma2000Tool(),
            new ConfigureTdScdmaTool(),
            new ConfigureSdmbTool(),
            new ConfigureLteFddTool(),
            new ConfigureLteTddTool(),
            new ConfigureWlanTool(),
            new ConfigureWimaxFixedTool(),
            new ConfigureWimaxMobileTool(),
            new ConfigureTdmbTool(),
            new ConfigureDigitalVideoTool(),
            new ConfigureBroadcastRadioTool(),
            new ConfigureAwgnTool(),
            new ConfigureImportIqTool(),
            new SelectPlotViewTool(),
            new SetProjectTool(),
            new CalculateWaveformTool(),
        };

        private abstract class ConfigureTool : IAppTool
        {
            public abstract string Name { get; }
            public abstract string Description { get; }
            public ToolEffect Effect => ToolEffect.Configure;
            public abstract JObject InputSchema { get; }
            public abstract Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct);

            protected static IAssistantConfigureHost Host(ToolContext ctx) => ctx.Get<IAssistantConfigureHost>();

            protected static ToolResult Done(JObject data, string fallback)
            {
                data = data ?? new JObject();
                string summary = (string)data["summary"] ?? fallback;
                return ToolResult.Ok(summary, data);
            }
        }

        private sealed class SetSourcePersonalityTool : ConfigureTool
        {
            public override string Name => "set_source_personality";
            public override string Description =>
                "Select the active source personality. Use list_personalities first to see valid names. " +
                "This resets the source configuration to that personality's defaults.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("personality", Schema.Str("personality name", new[] { "CW", "Multitone", "Multitone-Distortion", "Multi-Carrier", "CustomMod", "Pulse", "Jitter", "GSM-EDGE", "Bluetooth", "W-CDMA", "W-CDMA-HSPA", "cdma2000", "TD-SCDMA", "S-DMB", "LTE-FDD", "LTE-TDD", "WLAN", "WiMAX-Fixed", "WiMAX-Mobile", "T-DMB", "Digital-Video", "Broadcast-Radio", "AWGN", "Import-IQ" }), required: true));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                string name = (string)args["personality"];
                JObject data = Host(ctx).SetSourcePersonality(name);
                return Task.FromResult(Done(data, "Source personality set to " + name + "."));
            }
        }

        private sealed class ConfigureCwTool : ConfigureTool
        {
            public override string Name => "configure_cw";
            public override string Description =>
                "Configure the CW (single-tone) source: frequency offset from carrier (Hz), amplitude (dBFS, " +
                "0 = full scale, negative = backed off), and starting phase (degrees).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("offset_hz", Schema.Number("tone offset from carrier, Hz")),
                Schema.P("amplitude_dbfs", Schema.Number("amplitude in dBFS (<= 0)")),
                Schema.P("phase_deg", Schema.Number("starting phase, degrees")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("cw", args), "Configured CW."));
        }

        private sealed class ConfigureMultitoneTool : ConfigureTool
        {
            public override string Name => "configure_multitone";
            public override string Description =>
                "Configure the multitone source: number of tones, tone spacing (Hz, or auto), and the phase " +
                "strategy (Newman minimizes PAPR; Random; Zero is all-aligned and high PAPR).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("tone_count", Schema.Integer("number of tones (>= 1)"), required: true),
                Schema.P("spacing_hz", Schema.Number("tone spacing in Hz")),
                Schema.P("phase_strategy", Schema.Str("phase assignment", new[] { "Newman", "Random", "Zero" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("multitone", args), "Configured multitone."));
        }

        private sealed class ConfigureMultitoneDistortionTool : ConfigureTool
        {
            public override string Name => "configure_multitone_distortion";
            public override string Description =>
                "Configure the Multitone Distortion (IMD/NPR) source: number of tones (2..4097), tone spacing " +
                "(Hz), centre offset (Hz), phase preset (Parabolic minimizes PAPR; Random; Constant is aligned " +
                "and high PAPR), and an optional NPR notch (enable, width Hz, offset Hz from band centre). " +
                "Per-tone tables (magnitude dB / phase deg) cycle across the comb and override the uniform " +
                "power / phase preset. Pre-distortion (predistortion_enabled) subtracts the measured per-tone " +
                "magnitude/phase error tables from the base values to pre-invert a measured channel response.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("tone_count", Schema.Integer("number of tones (2..4097)"), required: true),
                Schema.P("tone_spacing_hz", Schema.Number("tone spacing, Hz")),
                Schema.P("center_offset_hz", Schema.Number("comb centre offset from baseband, Hz")),
                Schema.P("phase", Schema.Str("phase preset", new[] { "Parabolic", "Random", "Constant" })),
                Schema.P("notch_enabled", Schema.Bool("clear an NPR notch")),
                Schema.P("notch_width_hz", Schema.Number("NPR notch width, Hz")),
                Schema.P("notch_offset_hz", Schema.Number("NPR notch offset from comb centre, Hz")),
                Schema.P("per_tone_magnitude_db", Schema.Array(Schema.Number(), "per-tone magnitude table, dB (cyclic)")),
                Schema.P("per_tone_phase_deg", Schema.Array(Schema.Number(), "per-tone phase table, degrees (cyclic)")),
                Schema.P("predistortion_enabled", Schema.Bool("apply in-band pre-distortion correction")),
                Schema.P("measured_tone_magnitude_error_db", Schema.Array(Schema.Number(), "measured per-tone magnitude error, dB (cyclic)")),
                Schema.P("measured_tone_phase_error_deg", Schema.Array(Schema.Number(), "measured per-tone phase error, degrees (cyclic)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("multitone_distortion", args), "Configured multitone distortion."));
        }

        private sealed class ConfigureCustomModulationTool : ConfigureTool
        {
            public override string Name => "configure_custom_modulation";
            public override string Description =>
                "Configure a custom-modulated source: modulation format, symbol rate (Hz), pulse-shaping filter " +
                "and roll-off (alpha), and payload pattern.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("modulation", Schema.Str("modulation format", new[] { "BPSK", "QPSK", "QAM16", "QAM64", "QAM256" }), required: true),
                Schema.P("symbol_rate_hz", Schema.Number("symbol rate, Hz"), required: true),
                Schema.P("filter", Schema.Str("pulse-shaping filter", new[] { "RRC", "RC", "Gaussian", "None" })),
                Schema.P("alpha", Schema.Number("filter roll-off / BT (0..1)")),
                Schema.P("payload", Schema.Str("payload pattern", new[] { "PN9", "PN15", "Ones", "Zeros", "Random" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("custom_modulation", args), "Configured custom modulation."));
        }

        private sealed class ConfigurePulseTool : ConfigureTool
        {
            public override string Name => "configure_pulse";
            public override string Description =>
                "Configure the Pulse Building (radar/EW) source: pulse width (s), nominal pulse-repetition " +
                "interval (s), optional raised-cosine rise/fall (s), and intra-pulse modulation. FM formats " +
                "(LinearFmChirp / NonLinearFmChirp / FmStep) use chirp_bandwidth_hz; NonLinearFmChirp adds " +
                "nlfm_curvature (0=linear..<1); FmStep/AmStep use intra_pulse_step_count; Bpsk/Qpsk use " +
                "phase_code_chips + phase_code_seed; BarkerPhase uses barker_length (2/3/4/5/7/11/13); " +
                "FrankCode uses frank_order_n (length N²); PolyphaseP4 uses polyphase_length. PRI patterning: " +
                "pri_mode Fixed/Staggered/Jittered — Jittered adds pri_jitter_sec (peak) + pri_jitter_seed. " +
                "Per-pulse offset tables and the staggered-PRI pattern are set in the UI / project file.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("pulse_width_sec", Schema.Number("pulse width (on time), seconds"), required: true),
                Schema.P("pri_sec", Schema.Number("nominal pulse repetition interval, seconds (>= pulse width)"), required: true),
                Schema.P("rise_fall_sec", Schema.Number("raised-cosine edge time, seconds (0 = rectangular)")),
                Schema.P("modulation", Schema.Str("intra-pulse modulation", new[]
                {
                    "None", "LinearFmChirp", "NonLinearFmChirp", "FmStep", "AmStep",
                    "Bpsk", "Qpsk", "BarkerPhase", "FrankCode", "PolyphaseP4"
                })),
                Schema.P("chirp_bandwidth_hz", Schema.Number("swept bandwidth for the FM formats, Hz")),
                Schema.P("nlfm_curvature", Schema.Number("NonLinearFmChirp curvature, 0 (linear) .. <1")),
                Schema.P("intra_pulse_step_count", Schema.Integer("number of steps for FmStep / AmStep (>= 1)")),
                Schema.P("phase_code_chips", Schema.Integer("chip count for Bpsk / Qpsk phase codes (>= 1)")),
                Schema.P("phase_code_seed", Schema.Integer("seed for the Bpsk / Qpsk phase code")),
                Schema.P("barker_length", Schema.Integer("Barker code length (2,3,4,5,7,11,13)")),
                Schema.P("frank_order_n", Schema.Integer("Frank code order N (length N²)")),
                Schema.P("polyphase_length", Schema.Integer("P4 polyphase code length (>= 1)")),
                Schema.P("pri_mode", Schema.Str("PRI patterning", new[] { "Fixed", "Staggered", "Jittered" })),
                Schema.P("pri_jitter_sec", Schema.Number("peak PRI jitter for Jittered mode, seconds")),
                Schema.P("pri_jitter_seed", Schema.Integer("seed for the PRI jitter generator")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("pulse", args), "Configured pulse."));
        }

        private sealed class ConfigureJitterTool : ConfigureTool
        {
            public override string Name => "configure_jitter";
            public override string Description =>
                "Configure the Jitter Injection source: underlying clock rate (Hz); periodic jitter shape " +
                "(None/Sinusoidal/Square/Triangle/SawTooth/Exponential/Custom), rate (Hz) and amplitude (UI pk-pk); " +
                "optional random (Gaussian) jitter (enable, RMS in UI, seed); an optional SJ frequency sweep " +
                "(sweep_enabled, start/stop Hz, Linear/Logarithmic) that can follow a tolerance mask " +
                "(mask_standard None/Custom/G8251Oc48/G8251Oc192/G8251Oc768 — G.8251 values are approximate); " +
                "and an optional max_jitter_ui_pp cap. Composite = periodic + random.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("clock_rate_hz", Schema.Number("underlying clock/tone rate, Hz"), required: true),
                Schema.P("periodic_shape", Schema.Str("periodic jitter shape",
                    new[] { "None", "Sinusoidal", "Square", "Triangle", "SawTooth", "Exponential", "Custom" })),
                Schema.P("periodic_rate_hz", Schema.Number("periodic jitter rate, Hz")),
                Schema.P("periodic_ui_pp", Schema.Number("periodic jitter amplitude, UI peak-to-peak")),
                Schema.P("random_enabled", Schema.Bool("add Gaussian random jitter")),
                Schema.P("random_ui_rms", Schema.Number("random jitter std dev (RMS), UI")),
                Schema.P("random_seed", Schema.Integer("random jitter seed")),
                Schema.P("sweep_enabled", Schema.Bool("sweep the SJ frequency to trace a tolerance mask")),
                Schema.P("sweep_start_hz", Schema.Number("SJ sweep start frequency, Hz")),
                Schema.P("sweep_stop_hz", Schema.Number("SJ sweep stop frequency, Hz")),
                Schema.P("sweep_mode", Schema.Str("SJ sweep mode", new[] { "Linear", "Logarithmic" })),
                Schema.P("mask_standard", Schema.Str("tolerance mask",
                    new[] { "None", "Custom", "G8251Oc48", "G8251Oc192", "G8251Oc768" })),
                Schema.P("sweep_follow_mask", Schema.Bool("take the SJ amplitude from the mask during the sweep")),
                Schema.P("max_jitter_ui_pp", Schema.Number("optional cap on periodic amplitude, UI pk-pk (0 = none)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("jitter", args), "Configured jitter injection."));
        }

        private sealed class ConfigureBroadcastRadioTool : ConfigureTool
        {
            public override string Name => "configure_broadcast_radio";
            public override string Description =>
                "Configure the Broadcast Radio (FM) source: audio test-tone frequency (Hz), stereo (adds 19 kHz " +
                "pilot + 38 kHz subcarrier), peak deviation (Hz, 75k), sample rate (Hz), and length. Set rds=true " +
                "for the RDS 57 kHz data subcarrier (rds_deviation_hz ~2k).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("audio_tone_hz", Schema.Number("audio test-tone frequency, Hz")),
                Schema.P("stereo", Schema.Bool("stereo (pilot + subcarrier)")),
                Schema.P("peak_deviation_hz", Schema.Number("peak FM deviation, Hz")),
                Schema.P("sample_rate_hz", Schema.Number("sample rate, Hz")),
                Schema.P("rds", Schema.Bool("add the RDS 57 kHz data subcarrier")),
                Schema.P("rds_deviation_hz", Schema.Number("RDS deviation contribution, Hz (~2k)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("broadcast_radio", args), "Configured broadcast radio."));
        }

        private sealed class ConfigureDigitalVideoTool : ConfigureTool
        {
            public override string Name => "configure_digital_video";
            public override string Description =>
                "Configure the Digital Video (DVB-T COFDM) source: transmission mode (Mode2K/Mode8K), guard-interval " +
                "ratio (OneQuarter/OneEighth/OneSixteenth/OneThirtySecond), number of OFDM symbols, and modulation. " +
                "Set frame_structured=true to insert the standard DVB-T scattered pilots (boosted, shifting by 3 " +
                "each symbol).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("mode", Schema.Str("DVB-T mode", new[] { "Mode2K", "Mode8K" })),
                Schema.P("guard_interval", Schema.Str("guard-interval ratio",
                    new[] { "OneQuarter", "OneEighth", "OneSixteenth", "OneThirtySecond" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols")),
                Schema.P("modulation", Schema.Str("subcarrier modulation", new[] { "QPSK", "QAM16", "QAM64" })),
                Schema.P("frame_structured", Schema.Bool("insert the DVB-T scattered pilots")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("digital_video", args), "Configured digital video."));
        }

        private sealed class ConfigureTdmbTool : ConfigureTool
        {
            public override string Name => "configure_tdmb";
            public override string Description =>
                "Configure the T-DMB (DAB COFDM) source: DAB transmission mode (ModeI/II/III/IV, sets FFT/carriers/" +
                "guard at 2.048 MHz bandwidth), number of OFDM symbols, and data source. Set frame_structured=true " +
                "for a DAB transmission frame — a null symbol + phase-reference symbol (the sync channel) + " +
                "differentially-encoded DQPSK data symbols. Generic mode uses plain QPSK.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("mode", Schema.Str("DAB transmission mode", new[] { "ModeI", "ModeII", "ModeIII", "ModeIV" })),
                Schema.P("symbol_count", Schema.Integer("number of data OFDM symbols")),
                Schema.P("data", Schema.Str("payload data source", new[] { "PN9", "PN15", "PN23", "AllZeros", "AllOnes" })),
                Schema.P("frame_structured", Schema.Bool("build a DAB frame (null + phase-reference symbol + DQPSK)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("t_dmb", args), "Configured T-DMB."));
        }

        private sealed class ConfigureWimaxMobileTool : ConfigureTool
        {
            public override string Name => "configure_wimax_mobile";
            public override string Description =>
                "Configure the 802.16e mobile-WiMAX (scalable OFDMA) source: FFT size (Fft128/512/1024/2048 at " +
                "10.9375 kHz spacing), cyclic-prefix ratio, number of OFDM symbols, and modulation. Set " +
                "frame_structured=true for a DL-OFDMA-style frame with a DL-PUSC pilot pattern and an optional " +
                "include_preamble (every-3rd-subcarrier OFDMA preamble symbol).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("fft_size", Schema.Str("scalable FFT size", new[] { "Fft128", "Fft512", "Fft1024", "Fft2048" })),
                Schema.P("cyclic_prefix_ratio", Schema.Str("CP ratio",
                    new[] { "OneQuarter", "OneEighth", "OneSixteenth", "OneThirtySecond" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols")),
                Schema.P("modulation", Schema.Str("subcarrier modulation", new[] { "QPSK", "QAM16", "QAM64" })),
                Schema.P("frame_structured", Schema.Bool("build a DL-OFDMA frame with pilots + optional preamble")),
                Schema.P("include_preamble", Schema.Bool("prepend the OFDMA downlink preamble symbol")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("wimax_mobile", args), "Configured mobile WiMAX."));
        }

        private sealed class ConfigureWimaxFixedTool : ConfigureTool
        {
            public override string Name => "configure_wimax_fixed";
            public override string Description =>
                "Configure the 802.16-2004 fixed-WiMAX (256-FFT OFDM) source: channel bandwidth (Hz), cyclic-prefix " +
                "ratio (OneQuarter/OneEighth/OneSixteenth/OneThirtySecond), number of OFDM symbols, and modulation. " +
                "Set frame_structured=true for the exact 256-FFT pilot map (±13/±38/±63/±88) with an optional " +
                "include_preamble (downlink preamble symbol).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("channel_bandwidth_hz", Schema.Number("nominal channel bandwidth, Hz")),
                Schema.P("cyclic_prefix_ratio", Schema.Str("CP ratio",
                    new[] { "OneQuarter", "OneEighth", "OneSixteenth", "OneThirtySecond" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols")),
                Schema.P("modulation", Schema.Str("subcarrier modulation", new[] { "BPSK", "QPSK", "QAM16", "QAM64" })),
                Schema.P("frame_structured", Schema.Bool("build a frame with the exact pilot map + optional preamble")),
                Schema.P("include_preamble", Schema.Bool("prepend the downlink preamble symbol")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("wimax_fixed", args), "Configured fixed WiMAX."));
        }

        private sealed class ConfigureWlanTool : ConfigureTool
        {
            public override string Name => "configure_wlan";
            public override string Description =>
                "Configure the 802.11 WLAN (OFDM) source: bandwidth (Bw20MHz = 64-FFT, Bw40MHz = 128-FFT at " +
                "312.5 kHz spacing), number of OFDM symbols, and subcarrier modulation. Set frame_structured=true " +
                "(20 MHz only) for a representative 802.11a/g PPDU with pilot subcarriers, a selectable " +
                "guard_interval (Long/Short), and an optional include_ltf_preamble (L-LTF training field).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("bandwidth", Schema.Str("channel bandwidth", new[] { "Bw20MHz", "Bw40MHz" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols")),
                Schema.P("modulation", Schema.Str("subcarrier modulation", new[] { "BPSK", "QPSK", "QAM16", "QAM64", "QAM256" })),
                Schema.P("frame_structured", Schema.Bool("build a representative 802.11a/g PPDU (20 MHz)")),
                Schema.P("guard_interval", Schema.Str("data-symbol guard interval", new[] { "Long", "Short" })),
                Schema.P("include_ltf_preamble", Schema.Bool("prepend the L-LTF training preamble")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("wlan", args), "Configured WLAN."));
        }

        private sealed class ConfigureLteTddTool : ConfigureTool
        {
            public override string Name => "configure_lte_tdd";
            public override string Description =>
                "Configure the 3GPP LTE TDD source: channel bandwidth (selects FFT/occupied carriers at 15 kHz " +
                "spacing) and subcarrier/PDSCH modulation. Generic OFDM mode uses symbol_count. Set " +
                "frame_structured=true for a proper E-UTRA TDD downlink frame (PSS/SSS/CRS + PDSCH, with the " +
                "D/S/U subframe pattern): then set cyclic_prefix (Normal/Extended), physical_cell_id (0..503), " +
                "subframe_count (10 = one frame), tdd_ul_dl_config (0..6), and tdd_special_subframe_config (0..9).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("bandwidth", Schema.Str("channel bandwidth",
                    new[] { "Bw1_4MHz", "Bw3MHz", "Bw5MHz", "Bw10MHz", "Bw15MHz", "Bw20MHz" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols (generic mode)")),
                Schema.P("modulation", Schema.Str("subcarrier/PDSCH modulation", new[] { "QPSK", "QAM16", "QAM64", "QAM256" })),
                Schema.P("frame_structured", Schema.Bool("build a proper E-UTRA TDD downlink frame")),
                Schema.P("cyclic_prefix", Schema.Str("cyclic prefix (frame mode)", new[] { "Normal", "Extended" })),
                Schema.P("physical_cell_id", Schema.Integer("physical-layer cell ID N_cell_ID (0..503)")),
                Schema.P("subframe_count", Schema.Integer("number of 1 ms subframes (frame mode; 10 = one radio frame)")),
                Schema.P("tdd_ul_dl_config", Schema.Integer("TDD uplink-downlink configuration (0..6)")),
                Schema.P("tdd_special_subframe_config", Schema.Integer("TDD special-subframe configuration (0..9)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("lte_tdd", args), "Configured LTE TDD."));
        }

        private sealed class ConfigureLteFddTool : ConfigureTool
        {
            public override string Name => "configure_lte_fdd";
            public override string Description =>
                "Configure the 3GPP LTE FDD source: channel bandwidth (selects FFT/occupied carriers at 15 kHz " +
                "spacing) and subcarrier/PDSCH modulation. Generic OFDM mode uses symbol_count. Set " +
                "frame_structured=true for a proper E-UTRA downlink frame (PSS/SSS/CRS + PDSCH): then set the " +
                "cyclic_prefix (Normal/Extended), physical_cell_id (0..503), and subframe_count (10 = one 10 ms frame).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("bandwidth", Schema.Str("channel bandwidth",
                    new[] { "Bw1_4MHz", "Bw3MHz", "Bw5MHz", "Bw10MHz", "Bw15MHz", "Bw20MHz" })),
                Schema.P("symbol_count", Schema.Integer("number of OFDM symbols (generic mode)")),
                Schema.P("modulation", Schema.Str("subcarrier/PDSCH modulation", new[] { "QPSK", "QAM16", "QAM64", "QAM256" })),
                Schema.P("frame_structured", Schema.Bool("build a proper E-UTRA downlink frame (PSS/SSS/CRS + PDSCH)")),
                Schema.P("cyclic_prefix", Schema.Str("cyclic prefix (frame mode)", new[] { "Normal", "Extended" })),
                Schema.P("physical_cell_id", Schema.Integer("physical-layer cell ID N_cell_ID (0..503)")),
                Schema.P("subframe_count", Schema.Integer("number of 1 ms subframes (frame mode; 10 = one radio frame)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("lte_fdd", args), "Configured LTE FDD."));
        }

        private sealed class ConfigureSdmbTool : ConfigureTool
        {
            public override string Name => "configure_s_dmb";
            public override string Description =>
                "Configure the S-DMB (CDM, approximate) source: chip rate (Hz), samples per chip, symbol count, " +
                "OVSF spreading factor and code index, RRC roll-off, and scrambling. NOTE: a representative CDM " +
                "signal, not a verified S-DMB waveform (air-interface unconfirmed).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("chip_rate_hz", Schema.Number("chip rate, Hz")),
                Schema.P("samples_per_chip", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of data symbols")),
                Schema.P("spreading_factor", Schema.Integer("OVSF spreading factor (power of 2)")),
                Schema.P("ovsf_index", Schema.Integer("OVSF code index")),
                Schema.P("rrc_beta", Schema.Number("RRC roll-off (0..1)")),
                Schema.P("scramble", Schema.Bool("apply complex scrambling")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("s_dmb", args), "Configured S-DMB."));
        }

        private sealed class ConfigureTdScdmaTool : ConfigureTool
        {
            public override string Name => "configure_td_scdma";
            public override string Description =>
                "Configure the TD-SCDMA source: chip rate (Hz, 1.28M), samples per chip, symbol count, OVSF " +
                "spreading factor and code index, modulation (QPSK/QAM16/QAM64), RRC roll-off (0.22), and scrambling. " +
                "Set code_channel_count > 1 to sum multiple code channels within a timeslot (<= SF).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("chip_rate_hz", Schema.Number("chip rate, Hz")),
                Schema.P("samples_per_chip", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of data symbols")),
                Schema.P("spreading_factor", Schema.Integer("OVSF spreading factor (power of 2)")),
                Schema.P("ovsf_index", Schema.Integer("OVSF code index (single-code mode)")),
                Schema.P("code_channel_count", Schema.Integer("code channels to sum (1 = single-code; <= SF)")),
                Schema.P("modulation", Schema.Str("data modulation", new[] { "QPSK", "QAM16", "QAM64" })),
                Schema.P("rrc_beta", Schema.Number("RRC roll-off (0..1)")),
                Schema.P("scramble", Schema.Bool("apply complex scrambling")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("td_scdma", args), "Configured TD-SCDMA."));
        }

        private sealed class ConfigureCdma2000Tool : ConfigureTool
        {
            public override string Name => "configure_cdma2000";
            public override string Description =>
                "Configure the 3GPP2 CDMA (cdma2000) source: chip rate (Hz, 1.2288M), samples per chip, symbol " +
                "count, Walsh spreading factor (power of two) and code index, modulation, roll-off, and scrambling. " +
                "Set code_channel_count > 1 to sum multiple Walsh code channels (forward-link composite, <= SF).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("chip_rate_hz", Schema.Number("chip rate, Hz")),
                Schema.P("samples_per_chip", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of data symbols")),
                Schema.P("spreading_factor", Schema.Integer("Walsh spreading factor (power of 2)")),
                Schema.P("ovsf_index", Schema.Integer("Walsh code index (single-code mode)")),
                Schema.P("code_channel_count", Schema.Integer("Walsh code channels to sum (1 = single-code; <= SF)")),
                Schema.P("modulation", Schema.Str("data modulation", new[] { "QPSK", "QAM16" })),
                Schema.P("rrc_beta", Schema.Number("pulse-shape roll-off (0..1)")),
                Schema.P("scramble", Schema.Bool("apply complex scrambling")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("cdma2000", args), "Configured cdma2000."));
        }

        private sealed class ConfigureHspaTool : ConfigureTool
        {
            public override string Name => "configure_wcdma_hspa";
            public override string Description =>
                "Configure the 3GPP W-CDMA HSPA (HS-PDSCH) source: chip rate (3.84M), samples per chip, symbol " +
                "count, OVSF spreading factor (16) and code index, HS modulation (QPSK/QAM16/QAM64), RRC roll-off, " +
                "and scrambling. Set code_channel_count > 1 to sum multiple HS-PDSCH multicodes (<= SF).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("chip_rate_hz", Schema.Number("chip rate, Hz")),
                Schema.P("samples_per_chip", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of data symbols")),
                Schema.P("spreading_factor", Schema.Integer("OVSF spreading factor (power of 2)")),
                Schema.P("ovsf_index", Schema.Integer("OVSF code index (single-code mode)")),
                Schema.P("code_channel_count", Schema.Integer("HS-PDSCH multicodes to sum (1 = single-code; <= SF)")),
                Schema.P("modulation", Schema.Str("HS modulation", new[] { "QPSK", "QAM16", "QAM64" })),
                Schema.P("rrc_beta", Schema.Number("RRC roll-off (0..1)")),
                Schema.P("scramble", Schema.Bool("apply complex scrambling")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("wcdma_hspa", args), "Configured W-CDMA HSPA."));
        }

        private sealed class ConfigureWcdmaTool : ConfigureTool
        {
            public override string Name => "configure_wcdma";
            public override string Description =>
                "Configure the 3GPP W-CDMA FDD source: chip rate (Hz, 3.84M), samples per chip, symbol count, " +
                "OVSF spreading factor (power of two) and code index, modulation, RRC roll-off (0.22), and " +
                "scrambling (enable + seed). Set code_channel_count > 1 for a multi-code downlink composite " +
                "(N orthogonal OVSF codes summed at equal power).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("chip_rate_hz", Schema.Number("chip rate, Hz")),
                Schema.P("samples_per_chip", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of data symbols")),
                Schema.P("spreading_factor", Schema.Integer("OVSF spreading factor (power of 2)")),
                Schema.P("ovsf_index", Schema.Integer("OVSF code index (single-code mode)")),
                Schema.P("code_channel_count", Schema.Integer("orthogonal code channels to sum (1 = single-code; <= SF)")),
                Schema.P("modulation", Schema.Str("data modulation", new[] { "QPSK", "QAM16", "QAM64" })),
                Schema.P("rrc_beta", Schema.Number("RRC roll-off (0..1)")),
                Schema.P("scramble", Schema.Bool("apply complex scrambling")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("wcdma_fdd", args), "Configured W-CDMA FDD."));
        }

        private sealed class ConfigureBluetoothTool : ConfigureTool
        {
            public override string Name => "configure_bluetooth";
            public override string Description =>
                "Configure the Bluetooth source: modulation (Gfsk = BR/LE constant-envelope; Edr2Mbps = EDR " +
                "π/4-DQPSK; Edr3Mbps = EDR 8-DPSK — EDR keeps 1 Msym/s), symbol rate (Hz), samples per symbol, " +
                "symbol count, GFSK modulation index (BR ~0.32, LE ~0.5), Gaussian BT (0.5), and data source.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("modulation", Schema.Str("modulation", new[] { "Gfsk", "Edr2Mbps", "Edr3Mbps" })),
                Schema.P("symbol_rate_hz", Schema.Number("symbol rate, Hz")),
                Schema.P("samples_per_symbol", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of symbols")),
                Schema.P("modulation_index", Schema.Number("GFSK modulation index")),
                Schema.P("bt", Schema.Number("Gaussian bandwidth-time product (GFSK)")),
                Schema.P("data", Schema.Str("payload data source", new[] { "PN9", "PN15", "PN23", "AllZeros", "AllOnes" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("bluetooth", args), "Configured Bluetooth."));
        }

        private sealed class ConfigureGsmEdgeTool : ConfigureTool
        {
            public override string Name => "configure_gsm_edge";
            public override string Description =>
                "Configure the GSM/EDGE source: modulation (Gmsk = GSM/GPRS constant-envelope; Edge8Psk = EDGE " +
                "3π/8-rotated 8-PSK), symbol rate (Hz, 270.833k), samples per symbol, symbol count, Gaussian BT " +
                "(GMSK, 0.3), and payload data source.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("modulation", Schema.Str("modulation", new[] { "Gmsk", "Edge8Psk" })),
                Schema.P("symbol_rate_hz", Schema.Number("symbol rate, Hz")),
                Schema.P("samples_per_symbol", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of symbols")),
                Schema.P("bt", Schema.Number("Gaussian bandwidth-time product (GMSK)")),
                Schema.P("data", Schema.Str("payload data source", new[] { "PN9", "PN15", "PN23", "AllZeros", "AllOnes" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("gsm_edge", args), "Configured GSM/EDGE."));
        }

        private sealed class ConfigureAwgnTool : ConfigureTool
        {
            public override string Name => "configure_awgn";
            public override string Description =>
                "Configure the AWGN (band-limited noise) source: noise bandwidth (Hz), carrier-to-noise ratio (dB), " +
                "and whether to clip peaks.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("bandwidth_hz", Schema.Number("noise bandwidth, Hz"), required: true),
                Schema.P("cn_db", Schema.Number("carrier-to-noise ratio, dB")),
                Schema.P("clip", Schema.Bool("clip peaks")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("awgn", args), "Configured AWGN."));
        }

        private sealed class ConfigureImportIqTool : ConfigureTool
        {
            public override string Name => "configure_import_iq";
            public override string Description =>
                "Configure the Import-IQ source from a file. IMPORTANT: file_path must be provided by the user — " +
                "do not invent paths. You may suggest format/sample-rate values. Set the file format (incl. " +
                "AgilentInt14Be for 14-bit big-endian and Mat for MATLAB Level-5 .mat), sample rate (Hz), and " +
                "optional marker authoring (marker_mode None/Start/Periodic/Range with period/start/length in samples).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("file_path", Schema.Str("path to the I/Q file (user-supplied)"), required: true),
                Schema.P("format", Schema.Str("container format",
                    new[] { "Auto", "DelimitedText", "RawInt16", "AgilentInt16Be", "AgilentInt14Be", "Wav", "Mat" })),
                Schema.P("sample_rate_hz", Schema.Number("source sample rate, Hz")),
                Schema.P("marker_mode", Schema.Str("marker authoring", new[] { "None", "Start", "Periodic", "Range" })),
                Schema.P("marker_period_samples", Schema.Integer("marker period for Periodic, samples")),
                Schema.P("marker_start_sample", Schema.Integer("marker block start for Range, samples")),
                Schema.P("marker_length_samples", Schema.Integer("marker block length for Range, samples")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("import_iq", args), "Configured Import-IQ."));
        }

        private sealed class SelectPlotViewTool : ConfigureTool
        {
            public override string Name => "select_plot_view";
            public override string Description =>
                "Set one of the three verification panes to a view so the user can see a specific aspect of the signal.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("pane", Schema.Str("which pane", new[] { "top", "middle", "bottom" }), required: true),
                Schema.P("view", Schema.Str("view to show", new[] { "IQ", "Spectrum", "Constellation", "Eye", "CCDF" }), required: true));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                string pane = (string)args["pane"], view = (string)args["view"];
                return Task.FromResult(Done(Host(ctx).SelectPlotView(pane, view), "Showing " + view + " on the " + pane + " pane."));
            }
        }

        private sealed class SetProjectTool : ConfigureTool
        {
            public override string Name => "set_project";
            public override string Description =>
                "Project action: save the current project, load a project, or reset to defaults. load/reset " +
                "discard unsaved changes. For save/load, path is the .ssproj file (user-supplied for load).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("action", Schema.Str("project action", new[] { "save", "load", "reset" }), required: true),
                Schema.P("path", Schema.Str("path to the .ssproj file (for save/load)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                string action = (string)args["action"], path = (string)args["path"];
                return Task.FromResult(Done(Host(ctx).SetProject(action, path), "Project " + action + " done."));
            }
        }

        private sealed class CalculateWaveformTool : ConfigureTool
        {
            public override string Name => "calculate_waveform";
            public override string Description =>
                "Run Calculate to generate the I/Q waveform from the current configuration. No hardware is touched. " +
                "Returns the results readout and validation findings. Safe to call autonomously.";
            public override JObject InputSchema => Schema.Object();

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).CalculateWaveform(), "Waveform calculated."));
        }
    }
}
