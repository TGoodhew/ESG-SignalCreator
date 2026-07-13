using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Analyzer measurement + verification tools (#90, §8/§9) — work with the connected VSA, an E4406A
    /// or a Keysight N9010A: get_vsa_state, measure_channel_power,
    /// measure_acp, measure_ccdf, measure_spectrum_peak, measure_waveform, and verify_signal. All are
    /// <see cref="ToolEffect.Read"/> — they read the analyzer and never emit RF, so they run without a
    /// confirmation. (They are excluded from the read-parallelization classifier so concurrent sweeps
    /// can't collide on the one analyzer.) They delegate to <see cref="IAssistantMeasureHost"/>.
    /// </summary>
    public static class MeasureTools
    {
        public static IEnumerable<IAppTool> All() => new IAppTool[]
        {
            new GetVsaStateTool(),
            new MeasureChannelPowerTool(),
            new MeasureAcpTool(),
            new MeasureCcdfTool(),
            new MeasureSpectrumPeakTool(),
            new MeasureWaveformTool(),
            new VerifySignalTool(),
        };

        private abstract class MeasureTool : IAppTool
        {
            public abstract string Name { get; }
            public abstract string Description { get; }
            public ToolEffect Effect => ToolEffect.Read;
            public abstract JObject InputSchema { get; }
            public abstract Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct);

            protected static IAssistantMeasureHost Host(ToolContext ctx) => ctx.Get<IAssistantMeasureHost>();
            protected static ToolResult Done(JObject data, string fallback) =>
                ToolResult.Ok((string)(data ?? new JObject())["summary"] ?? fallback, data ?? new JObject());
            protected static double Num(JObject a, string k, double dflt) => a[k] != null ? (double)a[k] : dflt;
            protected static double? Opt(JObject a, string k) => a[k] != null ? (double?)(double)a[k] : null;
        }

        private sealed class GetVsaStateTool : MeasureTool
        {
            public override string Name => "get_vsa_state";
            public override string Description => "Connected analyzer state (E4406A or N9010A): connection, *IDN?, installed options, and current measurement mode.";
            public override JObject InputSchema => Schema.Object();
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).GetVsaState(), "VSA state."));
        }

        private sealed class MeasureChannelPowerTool : MeasureTool
        {
            public override string Name => "measure_channel_power";
            public override string Description => "Measure total channel power (dBm) and PSD on the analyzer around a center frequency.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("center_hz", Schema.Number("center frequency, Hz"), required: true),
                Schema.P("span_hz", Schema.Number("measurement span, Hz (default 5e6)")));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).MeasureChannelPower(Num(a, "center_hz", 0), Num(a, "span_hz", 5e6)), "Channel power."));
        }

        private sealed class MeasureAcpTool : MeasureTool
        {
            public override string Name => "measure_acp";
            public override string Description => "Measure adjacent-channel power ratios (dBc) on the analyzer. Optional carrier integration bandwidth (Hz).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("center_hz", Schema.Number("center frequency, Hz"), required: true),
                Schema.P("carrier_bandwidth_hz", Schema.Number("carrier integration bandwidth, Hz")));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).MeasureAcp(Num(a, "center_hz", 0), Num(a, "carrier_bandwidth_hz", 0)), "ACP."));
        }

        private sealed class MeasureCcdfTool : MeasureTool
        {
            public override string Name => "measure_ccdf";
            public override string Description => "Measure CCDF / PAPR (dB) and average power on the analyzer (Power Statistics).";
            public override JObject InputSchema => Schema.Object(
                Schema.P("center_hz", Schema.Number("center frequency, Hz"), required: true));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).MeasureCcdf(Num(a, "center_hz", 0)), "CCDF / PAPR."));
        }

        private sealed class MeasureSpectrumPeakTool : MeasureTool
        {
            public override string Name => "measure_spectrum_peak";
            public override string Description => "Measure the spectrum peak (frequency Hz + power dBm) and occupied bandwidth on the analyzer.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("center_hz", Schema.Number("center frequency, Hz"), required: true),
                Schema.P("span_hz", Schema.Number("measurement span, Hz (default 5e6)")));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).MeasureSpectrumPeak(Num(a, "center_hz", 0), Num(a, "span_hz", 5e6)), "Spectrum peak."));
        }

        private sealed class MeasureWaveformTool : MeasureTool
        {
            public override string Name => "measure_waveform";
            public override string Description => "Measure time-domain peak/mean power (dBm) and peak-to-mean (dB) on the analyzer.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("center_hz", Schema.Number("center frequency, Hz"), required: true));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).MeasureWaveform(Num(a, "center_hz", 0)), "Waveform."));
        }

        private sealed class VerifySignalTool : MeasureTool
        {
            public override string Name => "verify_signal";
            public override string Description =>
                "Closed-loop verify: measure the played signal on the analyzer and compare expected-vs-measured " +
                "(channel power, PAPR from the generated I/Q, and — with a tone offset — tone frequency). Omitted " +
                "parameters default to the ESG's commanded carrier/power. Requires a calculated waveform + a connected analyzer.";
            public override JObject InputSchema => Schema.Object(
                Schema.P("carrier_hz", Schema.Number("carrier frequency, Hz (default: ESG setting)")),
                Schema.P("commanded_power_dbm", Schema.Number("commanded ESG power, dBm (default: ESG setting)")),
                Schema.P("tone_offset_hz", Schema.Number("tone offset from carrier, Hz (for a single tone)")));
            public override Task<ToolResult> ExecuteAsync(JObject a, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(Done(Host(ctx).VerifySignal(Opt(a, "carrier_hz"), Opt(a, "commanded_power_dbm"), Opt(a, "tone_offset_hz")), "Verification."));
        }
    }
}
