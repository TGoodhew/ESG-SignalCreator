using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Tools
{
    /// <summary>
    /// Opt-in, gated tools (#88, §6.4). <see cref="SendRawScpiTool"/> is the advanced escape hatch:
    /// registered DISABLED by default (enabled only via an explicit per-session setting), it is
    /// <see cref="ToolEffect.Hardware"/> and is in the always-confirm set so every call shows the literal
    /// command and requires approval; the host logs the command and the resulting <c>:SYSTem:ERRor?</c>.
    /// </summary>
    public static class GatedTools
    {
        /// <summary>The name under which the raw-SCPI tool is registered (used to enable/disable it).</summary>
        public const string SendRawScpiName = "send_raw_scpi";

        public static IAppTool SendRawScpi() => new SendRawScpiTool();

        private sealed class SendRawScpiTool : IAppTool
        {
            public string Name => SendRawScpiName;
            public string Description =>
                "ADVANCED escape hatch: send a literal SCPI command to the connected instrument and return " +
                "its response plus the instrument error queue. Use only when no dedicated tool exists; the " +
                "exact command is shown to the user for approval every time. Disabled unless the user has " +
                "turned on raw-SCPI passthrough.";
            public ToolEffect Effect => ToolEffect.Hardware;
            public JObject InputSchema => Schema.Object(
                Schema.P("command", Schema.Str("the literal SCPI command (a query ends with '?')"), required: true));

            public Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                JObject data = ctx.Get<IAssistantRawScpiHost>().SendRawScpi((string)args["command"]);
                return Task.FromResult(ToolResult.Ok((string)data["summary"] ?? "SCPI sent.", data));
            }
        }
    }
}
