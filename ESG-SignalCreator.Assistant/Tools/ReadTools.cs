using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// The v1 read tools (#81, §4.2): app state, personalities, current config, validation, and the
    /// results readout. All are <see cref="ToolEffect.Read"/> and never gated. Each formats a snapshot
    /// from <see cref="IAssistantReadHost"/> into structured JSON for Claude.
    /// </summary>
    public static class ReadTools
    {
        /// <summary>All read tools, for one-call registration.</summary>
        public static IEnumerable<IAppTool> All() => new IAppTool[]
        {
            new GetAppStateTool(),
            new ListPersonalitiesTool(),
            new GetCurrentConfigTool(),
            new GetValidationResultsTool(),
            new GetResultsReadoutTool(),
        };

        private abstract class ReadTool : IAppTool
        {
            public abstract string Name { get; }
            public abstract string Description { get; }
            public ToolEffect Effect => ToolEffect.Read;
            public virtual JObject InputSchema => Schema.Object();
            public abstract Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct);

            protected static IAssistantReadHost Host(ToolContext ctx) => ctx.Get<IAssistantReadHost>();
        }

        private sealed class GetAppStateTool : ReadTool
        {
            public override string Name => "get_app_state";
            public override string Description =>
                "Current app state: active source personality, instrument connection (online/offline, model, " +
                "installed options), pipeline stage, ARB memory used/available (samples), and last error. " +
                "Call this first to orient before configuring or acting.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                AppStateSnapshot s = Host(ctx).GetAppState();
                var data = new JObject
                {
                    ["personality"] = s.PersonalityName,
                    ["connected"] = s.Connected,
                    ["instrument_model"] = s.InstrumentModel,
                    ["instrument_options"] = s.InstrumentOptions != null ? new JArray(s.InstrumentOptions) : new JArray(),
                    ["pipeline_stage"] = s.PipelineStage,
                    ["memory_used_samples"] = s.MemoryUsedSamples,
                    ["memory_available_samples"] = s.MemoryAvailableSamples,
                    ["last_error"] = s.LastError
                };
                string summary = (s.Connected ? "Connected to " + (s.InstrumentModel ?? "instrument") : "Offline") +
                                 "; personality " + (s.PersonalityName ?? "none") + "; stage " + (s.PipelineStage ?? "idle") + ".";
                return Task.FromResult(ToolResult.Ok(summary, data));
            }
        }

        private sealed class ListPersonalitiesTool : ReadTool
        {
            public override string Name => "list_personalities";
            public override string Description =>
                "List the available source personalities (CW, Multitone, Custom Mod, AWGN, Import IQ, …) and " +
                "their configurable parameters. Use to discover what can be configured.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                var arr = new JArray();
                foreach (PersonalityInfo p in Host(ctx).ListPersonalities())
                    arr.Add(new JObject { ["name"] = p.Name, ["description"] = p.Description, ["parameters"] = p.Parameters ?? new JObject() });
                return Task.FromResult(ToolResult.Ok(arr.Count + " personalities available.", new JObject { ["personalities"] = arr }));
            }
        }

        private sealed class GetCurrentConfigTool : ReadTool
        {
            public override string Name => "get_current_config";
            public override string Description =>
                "The active source/impairment/sequence configuration as JSON (the project model). Use to read " +
                "exactly what is currently set before changing it.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                JObject cfg = Host(ctx).GetCurrentConfig() ?? new JObject();
                return Task.FromResult(ToolResult.Ok("Current configuration.", new JObject { ["config"] = cfg }));
            }
        }

        private sealed class GetValidationResultsTool : ReadTool
        {
            public override string Name => "get_validation_results";
            public override string Description =>
                "The dependency-checker findings for the current configuration (over-range, memory cap, minimum " +
                "samples, granularity, wrap). Each item has severity (Info/Warning/Error), message, and optional field. " +
                "Check before downloading.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                var arr = new JArray();
                int errors = 0, warnings = 0;
                foreach (ValidationResult v in Host(ctx).GetValidation())
                {
                    if (v.Severity == ValidationSeverity.Error) errors++;
                    else if (v.Severity == ValidationSeverity.Warning) warnings++;
                    arr.Add(new JObject { ["severity"] = v.Severity.ToString(), ["message"] = v.Message, ["field"] = v.Field });
                }
                var data = new JObject { ["results"] = arr, ["error_count"] = errors, ["warning_count"] = warnings };
                string summary = arr.Count == 0 ? "No validation findings." : errors + " error(s), " + warnings + " warning(s).";
                return Task.FromResult(ToolResult.Ok(summary, data));
            }
        }

        private sealed class GetResultsReadoutTool : ReadTool
        {
            public override string Name => "get_results_readout";
            public override string Description =>
                "The computed results readout for the last calculated waveform: sample count, duration (s), sample " +
                "rate (Hz), peak/RMS (dBFS), PAPR (dB), occupied bandwidth (Hz), and predicted DAC headroom (dB). " +
                "Returns calculated=false if nothing has been calculated yet.";

            public override Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                ReadoutSnapshot r = Host(ctx).GetReadout();
                if (r == null)
                    return Task.FromResult(ToolResult.Ok("No waveform calculated yet.", new JObject { ["calculated"] = false }));

                var data = new JObject
                {
                    ["calculated"] = true,
                    ["sample_count"] = r.SampleCount,
                    ["sample_rate_hz"] = r.SampleRateHz,
                    ["duration_seconds"] = r.DurationSeconds,
                    ["peak_dbfs"] = r.PeakDbfs,
                    ["rms_dbfs"] = r.RmsDbfs,
                    ["papr_db"] = r.PaprDb,
                    ["occupied_bw_hz"] = r.OccupiedBwHz,
                    ["dac_headroom_db"] = r.DacHeadroomDb
                };
                string summary = string.Format(CultureInfo.InvariantCulture,
                    "{0} samples @ {1:0.###} MHz ({2:0.###} ms), PAPR {3:0.##} dB.",
                    r.SampleCount, r.SampleRateHz / 1e6, r.DurationSeconds * 1e3, r.PaprDb);
                return Task.FromResult(ToolResult.Ok(summary, data));
            }
        }
    }
}
