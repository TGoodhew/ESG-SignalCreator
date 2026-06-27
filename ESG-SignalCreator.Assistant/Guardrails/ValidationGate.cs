using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Tools;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Guardrails
{
    /// <summary>
    /// Host hook for the pre-execution validation gate (#83, §6.3): independently re-run the Core
    /// dependency checker just before a hardware action. The App implements it against the same
    /// <c>WaveformValidator</c> the UI uses.
    /// </summary>
    public interface IValidationGateHost
    {
        IReadOnlyList<ValidationResult> RevalidateForHardware();
    }

    /// <summary>
    /// A pre-execution gate run before any non-read tool (#83). For hardware tools that commit a
    /// waveform to the instrument (download/play), it re-runs validation and refuses on any Error —
    /// regardless of user confirmation, so Claude can't talk the app past a hard validation failure.
    /// Returns null to allow, or a refusal reason to block.
    /// </summary>
    public interface IPreExecutionGate
    {
        Task<string> CheckAsync(IAppTool tool, JObject args, CancellationToken ct);
    }

    public sealed class ValidationGate : IPreExecutionGate
    {
        private readonly IValidationGateHost _host;
        private readonly HashSet<string> _guardedTools;

        public ValidationGate(IValidationGateHost host, IEnumerable<string> guardedTools = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _guardedTools = new HashSet<string>(
                guardedTools ?? new[] { "download_waveform", "play_rf" }, StringComparer.Ordinal);
        }

        public Task<string> CheckAsync(IAppTool tool, JObject args, CancellationToken ct)
        {
            if (tool == null || !_guardedTools.Contains(tool.Name))
                return Task.FromResult<string>(null);

            IReadOnlyList<ValidationResult> results = _host.RevalidateForHardware() ?? new List<ValidationResult>();
            List<ValidationResult> errors = results.Where(r => r.Severity == ValidationSeverity.Error).ToList();
            if (errors.Count == 0)
                return Task.FromResult<string>(null);

            string detail = string.Join("; ", errors.Select(e => e.Message));
            return Task.FromResult("validation failed, hardware action refused: " + detail);
        }
    }
}
