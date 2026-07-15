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
                Schema.P("personality", Schema.Str("personality name", new[] { "CW", "Multitone", "Multitone-Distortion", "Multi-Carrier", "CustomMod", "Pulse", "Jitter", "GSM-EDGE", "Bluetooth", "AWGN", "Import-IQ" }), required: true));

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
                "and high PAPR), and an optional NPR notch (enable, width Hz, offset Hz from band centre).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("tone_count", Schema.Integer("number of tones (2..4097)"), required: true),
                Schema.P("tone_spacing_hz", Schema.Number("tone spacing, Hz")),
                Schema.P("center_offset_hz", Schema.Number("comb centre offset from baseband, Hz")),
                Schema.P("phase", Schema.Str("phase preset", new[] { "Parabolic", "Random", "Constant" })),
                Schema.P("notch_enabled", Schema.Bool("clear an NPR notch")),
                Schema.P("notch_width_hz", Schema.Number("NPR notch width, Hz")),
                Schema.P("notch_offset_hz", Schema.Number("NPR notch offset from comb centre, Hz")));

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
                "Configure the Pulse Building (radar/EW) source: pulse width (s) and pulse-repetition interval " +
                "(s), optional raised-cosine rise/fall (s), and intra-pulse modulation (None; LinearFmChirp with " +
                "a swept bandwidth in Hz; or BarkerPhase with a code length of 2/3/4/5/7/11/13).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("pulse_width_sec", Schema.Number("pulse width (on time), seconds"), required: true),
                Schema.P("pri_sec", Schema.Number("pulse repetition interval, seconds (>= pulse width)"), required: true),
                Schema.P("rise_fall_sec", Schema.Number("raised-cosine edge time, seconds (0 = rectangular)")),
                Schema.P("modulation", Schema.Str("intra-pulse modulation", new[] { "None", "LinearFmChirp", "BarkerPhase" })),
                Schema.P("chirp_bandwidth_hz", Schema.Number("swept bandwidth for LinearFmChirp, Hz")),
                Schema.P("barker_length", Schema.Integer("Barker code length (2,3,4,5,7,11,13)")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("pulse", args), "Configured pulse."));
        }

        private sealed class ConfigureJitterTool : ConfigureTool
        {
            public override string Name => "configure_jitter";
            public override string Description =>
                "Configure the Jitter Injection source: underlying clock rate (Hz); periodic jitter shape " +
                "(None/Sinusoidal/Square/Triangle/SawTooth/Exponential), rate (Hz) and amplitude (UI pk-pk); " +
                "and optional random (Gaussian) jitter (enable, RMS in UI, seed). Composite = periodic + random.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("clock_rate_hz", Schema.Number("underlying clock/tone rate, Hz"), required: true),
                Schema.P("periodic_shape", Schema.Str("periodic jitter shape",
                    new[] { "None", "Sinusoidal", "Square", "Triangle", "SawTooth", "Exponential" })),
                Schema.P("periodic_rate_hz", Schema.Number("periodic jitter rate, Hz")),
                Schema.P("periodic_ui_pp", Schema.Number("periodic jitter amplitude, UI peak-to-peak")),
                Schema.P("random_enabled", Schema.Bool("add Gaussian random jitter")),
                Schema.P("random_ui_rms", Schema.Number("random jitter std dev (RMS), UI")),
                Schema.P("random_seed", Schema.Integer("random jitter seed")));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("jitter", args), "Configured jitter injection."));
        }

        private sealed class ConfigureBluetoothTool : ConfigureTool
        {
            public override string Name => "configure_bluetooth";
            public override string Description =>
                "Configure the Bluetooth (GFSK) source: symbol rate (Hz, BR/LE-1M = 1M, LE-2M = 2M), samples " +
                "per symbol, symbol count, modulation index (BR ~0.32, LE ~0.5), Gaussian BT (0.5), and data source.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("symbol_rate_hz", Schema.Number("symbol rate, Hz")),
                Schema.P("samples_per_symbol", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of symbols")),
                Schema.P("modulation_index", Schema.Number("GFSK modulation index")),
                Schema.P("bt", Schema.Number("Gaussian bandwidth-time product")),
                Schema.P("data", Schema.Str("payload data source", new[] { "PN9", "PN15", "PN23", "AllZeros", "AllOnes" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).Configure("bluetooth", args), "Configured Bluetooth."));
        }

        private sealed class ConfigureGsmEdgeTool : ConfigureTool
        {
            public override string Name => "configure_gsm_edge";
            public override string Description =>
                "Configure the GSM/EDGE (GMSK) source: symbol rate (Hz, GSM = 270.833k), samples per symbol, " +
                "symbol count, Gaussian BT (GSM = 0.3), and payload data source.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("symbol_rate_hz", Schema.Number("symbol/bit rate, Hz")),
                Schema.P("samples_per_symbol", Schema.Integer("oversampling factor")),
                Schema.P("symbol_count", Schema.Integer("number of symbols")),
                Schema.P("bt", Schema.Number("Gaussian bandwidth-time product")),
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
                "do not invent paths. You may suggest format/sample-rate/resample values. Set the file format, " +
                "sample rate (Hz), and whether to resample to the target clock.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("file_path", Schema.Str("path to the I/Q file (user-supplied)"), required: true),
                Schema.P("format", Schema.Str("container format", new[] { "Auto", "DelimitedText", "RawInt16", "AgilentInt16Be", "Wav" })),
                Schema.P("sample_rate_hz", Schema.Number("source sample rate, Hz")),
                Schema.P("resample", Schema.Bool("resample to the target sample clock")));

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
