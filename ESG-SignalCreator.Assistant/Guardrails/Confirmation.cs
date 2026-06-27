using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Guardrails
{
    /// <summary>What the user is being asked to approve (rendered as an inline action card, §6.2).</summary>
    public sealed class ToolConfirmationRequest
    {
        public ToolConfirmationRequest(IAppTool tool, JObject args)
        {
            Tool = tool;
            Args = args;
        }

        public IAppTool Tool { get; }
        public JObject Args { get; }
        public string ToolName => Tool?.Name;
        public ToolEffect Effect => Tool?.Effect ?? ToolEffect.Read;
    }

    /// <summary>
    /// The host's confirmation prompt (#83/#84 implement the inline Approve/Decline card). Only called
    /// for actions that require a human yes (hardware / destructive).
    /// </summary>
    public interface IHardwareConfirmer
    {
        Task<bool> ConfirmAsync(ToolConfirmationRequest request, CancellationToken ct);
    }

    public sealed class EffectPolicyOptions
    {
        /// <summary>Per-session "auto-approve hardware" toggle (§6.2). Defaults OFF.</summary>
        public bool AutoApproveHardware { get; set; }

        /// <summary>
        /// Tools that ALWAYS confirm even when auto-approve is on (RF emission / bus takeover are never
        /// silent). Destructive tools always confirm regardless.
        /// </summary>
        public HashSet<string> AlwaysConfirm { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "play_rf", "connect_instrument", "send_raw_scpi" };
    }

    /// <summary>
    /// The real confirmation policy (#83, §6.1). read/configure run with no prompt; hardware/destructive
    /// require an in-app yes via <see cref="IHardwareConfirmer"/>. A per-session auto-approve toggle can
    /// skip the prompt for ordinary hardware tools, but never for destructive ones or the always-confirm
    /// set (play_rf, connect_instrument). Enforced in the dispatcher, not the prompt.
    /// </summary>
    public sealed class EffectConfirmationPolicy : IConfirmationPolicy
    {
        private readonly IHardwareConfirmer _confirmer;
        private readonly EffectPolicyOptions _options;

        public EffectConfirmationPolicy(IHardwareConfirmer confirmer, EffectPolicyOptions options = null)
        {
            _confirmer = confirmer ?? throw new ArgumentNullException(nameof(confirmer));
            _options = options ?? new EffectPolicyOptions();
        }

        public EffectPolicyOptions Options => _options;

        public Task<bool> ConfirmAsync(IAppTool tool, JObject args, CancellationToken ct)
        {
            switch (tool.Effect)
            {
                case ToolEffect.Read:
                case ToolEffect.Configure:
                    return Task.FromResult(true);

                case ToolEffect.Hardware:
                    if (_options.AutoApproveHardware && !_options.AlwaysConfirm.Contains(tool.Name))
                        return Task.FromResult(true);
                    return _confirmer.ConfirmAsync(new ToolConfirmationRequest(tool, args), ct);

                case ToolEffect.Destructive:
                default:
                    // Destructive always confirms, regardless of auto-approve.
                    return _confirmer.ConfirmAsync(new ToolConfirmationRequest(tool, args), ct);
            }
        }
    }
}
