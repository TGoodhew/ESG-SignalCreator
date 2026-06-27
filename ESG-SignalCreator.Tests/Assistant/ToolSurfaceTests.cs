using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class ToolSurfaceTests
    {
        /// <summary>An echo tool: returns its 'value' arg; effect configurable for policy tests.</summary>
        private sealed class EchoTool : IAppTool
        {
            public EchoTool(ToolEffect effect = ToolEffect.Read) { Effect = effect; }
            public string Name => "echo";
            public string Description => "Echo the value back.";
            public ToolEffect Effect { get; }
            public JObject InputSchema => Schema.Object(Schema.P("value", Schema.Str("text to echo"), required: true));

            public Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct) =>
                Task.FromResult(ToolResult.Ok("echoed", new JObject { ["value"] = (string)args["value"] }));
        }

        private sealed class DenyPolicy : IConfirmationPolicy
        {
            public Task<bool> ConfirmAsync(IAppTool tool, JObject args, CancellationToken ct) => Task.FromResult(false);
        }

        private static JObject Result(ContentBlock block) => JObject.Parse(block.Content);

        [Fact]
        public void Schema_object_builds_properties_and_required()
        {
            JObject s = Schema.Object(
                Schema.P("freq_hz", Schema.Number("carrier in Hz"), required: true),
                Schema.P("label", Schema.Str()));

            Assert.Equal("object", (string)s["type"]);
            Assert.Equal("number", (string)s["properties"]["freq_hz"]["type"]);
            Assert.Contains("freq_hz", s["required"].Select(t => (string)t));
            Assert.DoesNotContain("label", s["required"].Select(t => (string)t));
        }

        [Fact]
        public void Registry_projects_enabled_tools_into_definitions()
        {
            var reg = new ToolRegistry().Register(new EchoTool());
            Assert.Single(reg.ToToolDefinitions());
            reg.SetEnabled("echo", false);
            Assert.Empty(reg.ToToolDefinitions());
            Assert.Null(reg.ByName("echo"));
        }

        [Fact]
        public async Task Dispatcher_executes_a_read_tool_and_packages_result()
        {
            var reg = new ToolRegistry().Register(new EchoTool());
            var dispatcher = new ToolDispatcher(reg, new ToolContext());

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("tu_1", "echo", new JObject { ["value"] = "hi" }), CancellationToken.None);

            Assert.Equal("tu_1", block.ToolUseId);
            Assert.NotEqual(true, block.IsError);
            JObject r = Result(block);
            Assert.Equal("ok", (string)r["status"]);
            Assert.Equal("echoed", (string)r["summary"]);
            Assert.Equal("hi", (string)r["value"]);
        }

        [Fact]
        public async Task Dispatcher_rejects_unknown_tool()
        {
            var dispatcher = new ToolDispatcher(new ToolRegistry(), new ToolContext());
            ContentBlock block = await dispatcher.InvokeAsync(ContentBlock.OfToolUse("t", "nope", new JObject()), CancellationToken.None);
            Assert.True(block.IsError);
            Assert.Contains("Unknown", Result(block)["summary"].ToString());
        }

        [Fact]
        public async Task Dispatcher_rejects_missing_required_argument()
        {
            var reg = new ToolRegistry().Register(new EchoTool());
            var dispatcher = new ToolDispatcher(reg, new ToolContext());
            ContentBlock block = await dispatcher.InvokeAsync(ContentBlock.OfToolUse("t", "echo", new JObject()), CancellationToken.None);
            Assert.True(block.IsError);
            Assert.Contains("required", Result(block)["summary"].ToString());
        }

        [Fact]
        public async Task Dispatcher_rejects_wrong_argument_type()
        {
            var reg = new ToolRegistry().Register(new EchoTool());
            var dispatcher = new ToolDispatcher(reg, new ToolContext());
            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "echo", new JObject { ["value"] = 123 }), CancellationToken.None);
            Assert.True(block.IsError);
            Assert.Contains("must be a string", Result(block)["summary"].ToString());
        }

        [Fact]
        public async Task Confirmation_policy_blocks_non_read_tools_when_declined()
        {
            var reg = new ToolRegistry().Register(new EchoTool(ToolEffect.Configure));
            var dispatcher = new ToolDispatcher(reg, new ToolContext(), new DenyPolicy());

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "echo", new JObject { ["value"] = "x" }), CancellationToken.None);

            Assert.True(block.IsError);
            Assert.Contains("declined", Result(block)["summary"].ToString());
        }

        [Fact]
        public async Task Read_tools_are_not_gated_by_the_policy()
        {
            var reg = new ToolRegistry().Register(new EchoTool(ToolEffect.Read));
            var dispatcher = new ToolDispatcher(reg, new ToolContext(), new DenyPolicy());

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "echo", new JObject { ["value"] = "x" }), CancellationToken.None);

            Assert.False(block.IsError == true);
            Assert.Equal("ok", (string)Result(block)["status"]);
        }

        [Fact]
        public void ToolContext_resolves_registered_services()
        {
            var ctx = new ToolContext();
            ctx.Register<IFormatProvider>(System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(ctx.TryGet(out IFormatProvider fp));
            Assert.NotNull(fp);
            Assert.Throws<InvalidOperationException>(() => ctx.Get<string>());
        }
    }
}
