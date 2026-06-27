using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Guardrails;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>#88: the gated raw-SCPI passthrough — off by default, always confirms, logs the command.</summary>
    public class GatedToolTests
    {
        private sealed class FakeRawHost : IAssistantRawScpiHost
        {
            public string Last;
            public JObject SendRawScpi(string command)
            {
                Last = command;
                return new JObject { ["command"] = command, ["response"] = "1.0", ["error"] = "+0,\"No error\"", ["summary"] = "ok" };
            }
        }

        private sealed class ConfirmerSpy : IHardwareConfirmer
        {
            public int Calls;
            public bool Answer = true;
            public Task<bool> ConfirmAsync(ToolConfirmationRequest request, CancellationToken ct) { Calls++; return Task.FromResult(Answer); }
        }

        private sealed class NoErrorsGate : IValidationGateHost
        {
            public System.Collections.Generic.IReadOnlyList<EsgSignalCreator.Validation.ValidationResult> RevalidateForHardware() =>
                new System.Collections.Generic.List<EsgSignalCreator.Validation.ValidationResult>();
        }

        private static JObject Body(ContentBlock b) => JObject.Parse(b.Content);

        [Fact]
        public void Tool_is_hardware_and_requires_a_command()
        {
            IAppTool tool = GatedTools.SendRawScpi();
            Assert.Equal("send_raw_scpi", tool.Name);
            Assert.Equal(ToolEffect.Hardware, tool.Effect);
            Assert.Contains("command", tool.InputSchema["required"].Select(t => (string)t));
        }

        [Fact]
        public void Registered_disabled_by_default_is_hidden_and_unresolvable()
        {
            var reg = new ToolRegistry().Register(GatedTools.SendRawScpi());
            reg.SetEnabled(GatedTools.SendRawScpiName, false);
            Assert.Null(reg.ByName("send_raw_scpi"));
            Assert.DoesNotContain(reg.ToToolDefinitions(), d => d.Name == "send_raw_scpi");
        }

        [Fact]
        public async Task Dispatcher_rejects_the_disabled_tool()
        {
            var reg = new ToolRegistry().Register(GatedTools.SendRawScpi());
            reg.SetEnabled(GatedTools.SendRawScpiName, false);
            var ctx = new ToolContext();
            ctx.Register<IAssistantRawScpiHost>(new FakeRawHost());
            var dispatcher = new ToolDispatcher(reg, ctx);

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "send_raw_scpi", new JObject { ["command"] = "*IDN?" }), default);

            Assert.True(block.IsError);
            Assert.Contains("disabled", (string)Body(block)["summary"]);
        }

        [Fact]
        public async Task Always_confirms_even_under_auto_approve()
        {
            var host = new FakeRawHost();
            var reg = new ToolRegistry().Register(GatedTools.SendRawScpi());
            reg.SetEnabled(GatedTools.SendRawScpiName, true);
            var ctx = new ToolContext();
            ctx.Register<IAssistantRawScpiHost>(host);
            var spy = new ConfirmerSpy { Answer = false };
            var dispatcher = new ToolDispatcher(reg, ctx,
                new EffectConfirmationPolicy(spy, new EffectPolicyOptions { AutoApproveHardware = true }), new ValidationGate(new NoErrorsGate()));

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "send_raw_scpi", new JObject { ["command"] = "*RST" }), default);

            Assert.Equal(1, spy.Calls);       // raw SCPI always prompts, even with auto-approve
            Assert.True(block.IsError);        // declined
            Assert.Null(host.Last);            // never sent
        }

        [Fact]
        public async Task Approved_call_reaches_the_host_and_returns_response()
        {
            var host = new FakeRawHost();
            var reg = new ToolRegistry().Register(GatedTools.SendRawScpi());
            reg.SetEnabled(GatedTools.SendRawScpiName, true);
            var ctx = new ToolContext();
            ctx.Register<IAssistantRawScpiHost>(host);
            var dispatcher = new ToolDispatcher(reg, ctx,
                new EffectConfirmationPolicy(new ConfirmerSpy { Answer = true }), new ValidationGate(new NoErrorsGate()));

            ContentBlock block = await dispatcher.InvokeAsync(
                ContentBlock.OfToolUse("t", "send_raw_scpi", new JObject { ["command"] = "*IDN?" }), default);

            Assert.NotEqual(true, block.IsError);
            Assert.Equal("*IDN?", host.Last);
            Assert.Equal("1.0", (string)Body(block)["response"]);
        }
    }
}
