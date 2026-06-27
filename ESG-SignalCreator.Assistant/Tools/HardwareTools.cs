using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// The v1 hardware tools (#86, §4.2): connect/disconnect, download, play/stop, and instrument
    /// settings. All are <see cref="ToolEffect.Hardware"/>, so the dispatcher requires an in-app
    /// confirmation (play_rf and connect_instrument always confirm, even with auto-approve) and the
    /// validation gate re-checks before download_waveform / play_rf. Tools delegate to
    /// <see cref="IAssistantHardwareHost"/>; the host enforces the analyzer input-damage gate on power.
    /// </summary>
    public static class HardwareTools
    {
        public static IEnumerable<IAppTool> All() => new IAppTool[]
        {
            new ConnectInstrumentTool(),
            new DisconnectInstrumentTool(),
            new SetInstrumentSettingsTool(),
            new DownloadWaveformTool(),
            new PlayRfTool(),
            new StopRfTool(),
        };

        private abstract class HardwareTool : IAppTool
        {
            public abstract string Name { get; }
            public abstract string Description { get; }
            public ToolEffect Effect => ToolEffect.Hardware;
            public virtual JObject InputSchema => Schema.Object();
            public abstract Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct);

            protected static IAssistantHardwareHost Host(ToolContext ctx) => ctx.Get<IAssistantHardwareHost>();

            protected static ToolResult Done(JObject data, string fallback)
            {
                data = data ?? new JObject();
                return ToolResult.Ok((string)data["summary"] ?? fallback, data);
            }
        }

        private sealed class ConnectInstrumentTool : HardwareTool
        {
            public override string Name => "connect_instrument";
            public override string Description =>
                "Open a VISA session to the signal generator at the given resource and read *IDN?. " +
                "ALWAYS confirms (a bus takeover). The resource must be supplied by the user (e.g. " +
                "TCPIP0::192.168.1.82::inst1::INSTR or GPIB0::19::INSTR) — do not invent it.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("resource", Schema.Str("VISA resource string (user-supplied)"), required: true));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).ConnectInstrument((string)args["resource"]), "Connected."));
        }

        private sealed class DisconnectInstrumentTool : HardwareTool
        {
            public override string Name => "disconnect_instrument";
            public override string Description => "Close the open instrument session.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).DisconnectInstrument(), "Disconnected."));
        }

        private sealed class SetInstrumentSettingsTool : HardwareTool
        {
            public override string Name => "set_instrument_settings";
            public override string Description =>
                "Set instrument parameters on the connected generator: carrier frequency (Hz), output power " +
                "(dBm), RF on/off, modulation on/off, ARB sample clock (Hz), runtime scaling (%), and reference " +
                "(internal/external). Only the fields you provide are changed. Power is checked against the " +
                "analyzer input-damage limit before it is applied.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("frequency_hz", Schema.Number("carrier frequency, Hz")),
                Schema.P("power_dbm", Schema.Number("output power, dBm")),
                Schema.P("rf_on", Schema.Bool("RF output on/off")),
                Schema.P("modulation_on", Schema.Bool("modulation on/off")),
                Schema.P("sample_clock_hz", Schema.Number("ARB sample clock, Hz")),
                Schema.P("runtime_scaling_percent", Schema.Number("ARB runtime scaling, %")),
                Schema.P("reference", Schema.Str("timebase", new[] { "internal", "external" })));

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).SetInstrumentSettings(args), "Instrument settings applied."));
        }

        private sealed class DownloadWaveformTool : HardwareTool
        {
            public override string Name => "download_waveform";
            public override string Description =>
                "Download the current ARB waveform to the instrument's volatile memory. Requires a calculated " +
                "waveform and a connection; the app re-validates first and refuses on a hard validation error.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).DownloadWaveform(), "Waveform downloaded."));
        }

        private sealed class PlayRfTool : HardwareTool
        {
            public override string Name => "play_rf";
            public override string Description =>
                "Arm the ARB and turn the RF output ON. ALWAYS confirms — this emits RF. Requires a downloaded waveform.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).PlayRf(), "Playing."));
        }

        private sealed class StopRfTool : HardwareTool
        {
            public override string Name => "stop_rf";
            public override string Description => "Stop the ARB and turn the RF output OFF.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).StopRf(), "Stopped."));
        }
    }
}
