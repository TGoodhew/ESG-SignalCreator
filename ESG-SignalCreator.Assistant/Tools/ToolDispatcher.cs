using System;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Agent;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Guardrails;
using EsgSignalCreator.Assistant.Secrets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Turns a model tool_use into a tool_result (#80). It is the single choke point that: resolves the
    /// tool, validates arguments against its schema, applies the confirmation policy (§6) for any
    /// non-read effect, executes against Core, and packages the structured result. Implements
    /// <see cref="IToolInvoker"/> so the <see cref="AgentLoop"/> drives it directly.
    /// </summary>
    public sealed class ToolDispatcher : IToolInvoker
    {
        private readonly ToolRegistry _registry;
        private readonly ToolContext _context;
        private readonly IConfirmationPolicy _confirmation;
        private readonly IPreExecutionGate _gate;

        public ToolDispatcher(ToolRegistry registry, ToolContext context, IConfirmationPolicy confirmation = null, IPreExecutionGate gate = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _confirmation = confirmation ?? new AllowAllConfirmationPolicy();
            _gate = gate;
        }

        public async Task<ContentBlock> InvokeAsync(ContentBlock toolUse, CancellationToken ct)
        {
            if (toolUse == null) throw new ArgumentNullException(nameof(toolUse));
            string id = toolUse.Id;

            IAppTool tool = _registry.ByName(toolUse.Name);
            if (tool == null)
                return Error(id, "Unknown or disabled tool '" + toolUse.Name + "'.");

            JObject args = toolUse.Input ?? new JObject();

            string validation = SchemaValidator.Validate(tool.InputSchema, args);
            if (validation != null)
                return Error(id, "Invalid arguments for '" + tool.Name + "': " + validation + ".");

            // Pre-execution gate (e.g. re-run validation before a hardware action). A refusal here
            // blocks the action regardless of any later confirmation (§6.3).
            if (_gate != null && tool.Effect != ToolEffect.Read)
            {
                string refusal;
                try { refusal = await _gate.CheckAsync(tool, args, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { return Error(id, "Pre-execution gate failed for '" + tool.Name + "': " + ex.Message); }

                if (refusal != null)
                    return Error(id, refusal);
            }

            if (tool.Effect != ToolEffect.Read)
            {
                bool approved;
                try { approved = await _confirmation.ConfirmAsync(tool, args, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { return Error(id, "Confirmation failed for '" + tool.Name + "': " + ex.Message); }

                if (!approved)
                    return Error(id, "The user declined the '" + tool.Name + "' action.");
            }

            try
            {
                ToolResult result = await tool.ExecuteAsync(args, _context, ct).ConfigureAwait(false);
                return Package(id, result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(id, "Tool '" + tool.Name + "' failed: " + ex.Message);
            }
        }

        private static ContentBlock Package(string toolUseId, ToolResult result)
        {
            result = result ?? ToolResult.Error("Tool returned no result.");
            // Privacy minimization (§8): collapse any over-long arrays / huge strings so bulk data
            // (e.g. raw I/Q) can never leak into the conversation.
            var payload = result.Data != null ? PrivacyGuard.Compact(result.Data) : new JObject();
            payload["status"] = result.IsError ? "error" : "ok";
            if (result.Summary != null) payload["summary"] = result.Summary;
            string content = payload.ToString(Formatting.None);
            return ContentBlock.OfToolResult(toolUseId, content, result.IsError);
        }

        private static ContentBlock Error(string toolUseId, string message)
        {
            var payload = new JObject { ["status"] = "error", ["summary"] = message };
            return ContentBlock.OfToolResult(toolUseId, payload.ToString(Formatting.None), isError: true);
        }
    }
}
