using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Side-effect class of a tool (#80, §4.1). The dispatcher uses it to enforce the confirmation
    /// policy: <see cref="Read"/> runs freely; <see cref="Configure"/> changes app/project state only;
    /// <see cref="Hardware"/> touches the instrument; <see cref="Destructive"/> is irreversible.
    /// </summary>
    public enum ToolEffect
    {
        Read,
        Configure,
        Hardware,
        Destructive
    }

    /// <summary>The structured result of a tool execution (§4.3).</summary>
    public sealed class ToolResult
    {
        public bool IsError { get; set; }

        /// <summary>One line Claude can relay to the user.</summary>
        public string Summary { get; set; }

        /// <summary>Structured payload (merged with status + summary into the tool_result JSON).</summary>
        public JObject Data { get; set; }

        public static ToolResult Ok(string summary, JObject data = null) =>
            new ToolResult { IsError = false, Summary = summary, Data = data };

        public static ToolResult Error(string summary, JObject data = null) =>
            new ToolResult { IsError = true, Summary = summary, Data = data };
    }

    /// <summary>
    /// Service container handed to tools so they call the same Core/app services the UI uses — never
    /// WinForms control state. The host (App) registers concrete services; tools resolve them by type.
    /// Keeping this a locator avoids an Assistant→App dependency (App depends on Assistant, not the reverse).
    /// </summary>
    public sealed class ToolContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out object o)) { service = (T)o; return true; }
            service = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            if (TryGet(out T s)) return s;
            throw new InvalidOperationException("No service registered for " + typeof(T).FullName + ".");
        }
    }

    /// <summary>One tool exposed to Claude (§4.3). Implementations execute against Core, not the UI.</summary>
    public interface IAppTool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        ToolEffect Effect { get; }
        Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct);
    }

    /// <summary>
    /// Guardrail seam (#83 implements the real policy). The dispatcher asks before any non-read tool.
    /// </summary>
    public interface IConfirmationPolicy
    {
        Task<bool> ConfirmAsync(IAppTool tool, JObject args, CancellationToken ct);
    }

    /// <summary>Default policy: allow everything (used until #83 installs the real guardrail).</summary>
    public sealed class AllowAllConfirmationPolicy : IConfirmationPolicy
    {
        public Task<bool> ConfirmAsync(IAppTool tool, JObject args, CancellationToken ct) => Task.FromResult(true);
    }
}
