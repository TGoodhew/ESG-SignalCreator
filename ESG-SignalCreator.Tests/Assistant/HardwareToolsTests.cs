using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Guardrails;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class HardwareToolsTests
    {
        private sealed class FakeHardwareHost : IAssistantHardwareHost
        {
            public readonly List<string> Calls = new List<string>();
            public JObject LastSettings;

            public JObject ConnectInstrument(string resource) { Calls.Add("connect:" + resource); return new JObject { ["connected"] = true }; }
            public JObject DisconnectInstrument() { Calls.Add("disconnect"); return new JObject { ["disconnected"] = true }; }
            public JObject DownloadWaveform() { Calls.Add("download"); return new JObject { ["bytes"] = 16384 }; }
            public JObject PlayRf() { Calls.Add("play"); return new JObject { ["playing"] = true }; }
            public JObject StopRf() { Calls.Add("stop"); return new JObject { ["stopped"] = true }; }
            public JObject SetInstrumentSettings(JObject args) { Calls.Add("settings"); LastSettings = args; return new JObject { ["applied"] = new JArray("frequency_hz") }; }
        }

        private sealed class ConfirmerSpy : IHardwareConfirmer
        {
            public int Calls;
            public bool Answer = true;
            public Task<bool> ConfirmAsync(ToolConfirmationRequest request, CancellationToken ct) { Calls++; return Task.FromResult(Answer); }
        }

        private sealed class GateHost : IValidationGateHost
        {
            public List<ValidationResult> Results = new List<ValidationResult>();
            public IReadOnlyList<ValidationResult> RevalidateForHardware() => Results;
        }

        private static IAppTool Tool(string name) => HardwareTools.All().Single(t => t.Name == name);
        private static JObject Body(ContentBlock b) => JObject.Parse(b.Content);

        private static ToolDispatcher Dispatcher(FakeHardwareHost host, IHardwareConfirmer confirmer, IValidationGateHost gateHost, EffectPolicyOptions opts = null)
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantHardwareHost>(host);
            var reg = new ToolRegistry();
            reg.Register(HardwareTools.All());
            return new ToolDispatcher(reg, ctx, new EffectConfirmationPolicy(confirmer, opts), new ValidationGate(gateHost));
        }

        [Fact]
        public void All_hardware_tools_are_hardware_effect()
        {
            Assert.All(HardwareTools.All(), t => Assert.Equal(ToolEffect.Hardware, t.Effect));
            Assert.Equal(6, HardwareTools.All().Count());
        }

        [Fact]
        public void Connect_requires_a_resource_in_schema()
        {
            Assert.Contains("resource", Tool("connect_instrument").InputSchema["required"].Select(t => (string)t));
        }

        [Fact]
        public async Task Each_tool_delegates_to_its_host_method()
        {
            var host = new FakeHardwareHost();
            var ctx = new ToolContext();
            ctx.Register<IAssistantHardwareHost>(host);

            await Tool("connect_instrument").ExecuteAsync(new JObject { ["resource"] = "TCPIP0::1.2.3.4::inst0::INSTR" }, ctx, default);
            await Tool("disconnect_instrument").ExecuteAsync(new JObject(), ctx, default);
            await Tool("download_waveform").ExecuteAsync(new JObject(), ctx, default);
            await Tool("play_rf").ExecuteAsync(new JObject(), ctx, default);
            await Tool("stop_rf").ExecuteAsync(new JObject(), ctx, default);
            await Tool("set_instrument_settings").ExecuteAsync(new JObject { ["frequency_hz"] = 1e9 }, ctx, default);

            Assert.Equal(new[] { "connect:TCPIP0::1.2.3.4::inst0::INSTR", "disconnect", "download", "play", "stop", "settings" }, host.Calls.ToArray());
            Assert.Equal(1e9, (double)host.LastSettings["frequency_hz"], 0);
        }

        [Fact]
        public async Task Hardware_tool_runs_nothing_until_the_user_approves()
        {
            var host = new FakeHardwareHost();
            var spy = new ConfirmerSpy { Answer = false };
            ToolDispatcher d = Dispatcher(host, spy, new GateHost());

            ContentBlock block = await d.InvokeAsync(ContentBlock.OfToolUse("t", "download_waveform", new JObject()), default);

            Assert.True(block.IsError);
            Assert.Contains("declined", (string)Body(block)["summary"]);
            Assert.Empty(host.Calls); // nothing reached the instrument before approval
        }

        [Fact]
        public async Task Validation_error_refuses_download_before_confirmation()
        {
            var host = new FakeHardwareHost();
            var spy = new ConfirmerSpy { Answer = true };
            var gate = new GateHost { Results = { new ValidationResult(ValidationSeverity.Error, "memory cap exceeded") } };
            ToolDispatcher d = Dispatcher(host, spy, gate);

            ContentBlock block = await d.InvokeAsync(ContentBlock.OfToolUse("t", "download_waveform", new JObject()), default);

            Assert.True(block.IsError);
            Assert.Contains("validation failed", (string)Body(block)["summary"]);
            Assert.Equal(0, spy.Calls);   // never asked
            Assert.Empty(host.Calls);     // never ran
        }

        [Fact]
        public async Task Play_rf_always_confirms_even_with_auto_approve()
        {
            var host = new FakeHardwareHost();
            var spy = new ConfirmerSpy { Answer = false };
            ToolDispatcher d = Dispatcher(host, spy, new GateHost(), new EffectPolicyOptions { AutoApproveHardware = true });

            ContentBlock block = await d.InvokeAsync(ContentBlock.OfToolUse("t", "play_rf", new JObject()), default);

            Assert.Equal(1, spy.Calls);   // RF emission always prompts
            Assert.True(block.IsError);
            Assert.Empty(host.Calls);
        }

        [Fact]
        public async Task Ordinary_hardware_runs_without_prompt_when_auto_approved()
        {
            var host = new FakeHardwareHost();
            var spy = new ConfirmerSpy();
            ToolDispatcher d = Dispatcher(host, spy, new GateHost(), new EffectPolicyOptions { AutoApproveHardware = true });

            ContentBlock block = await d.InvokeAsync(
                ContentBlock.OfToolUse("t", "set_instrument_settings", new JObject { ["power_dbm"] = -10.0 }), default);

            Assert.Equal(0, spy.Calls);            // auto-approved (not in always-confirm set)
            Assert.NotEqual(true, block.IsError);
            Assert.Contains("settings", host.Calls);
        }

        [Fact]
        public async Task Approved_download_reaches_the_host()
        {
            var host = new FakeHardwareHost();
            var spy = new ConfirmerSpy { Answer = true };
            ToolDispatcher d = Dispatcher(host, spy, new GateHost());

            ContentBlock block = await d.InvokeAsync(ContentBlock.OfToolUse("t", "download_waveform", new JObject()), default);

            Assert.NotEqual(true, block.IsError);
            Assert.Contains("download", host.Calls);
            Assert.Equal(16384, (int)Body(block)["bytes"]);
        }
    }
}
