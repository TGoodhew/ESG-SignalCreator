using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Agent;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Guardrails;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>
    /// End-to-end acceptance over the assembled tool surface (#87, §11): scripted model turns driven
    /// through the real AgentLoop + ToolDispatcher + full registry (read + configure + hardware) against
    /// fake hosts — single-tool turns, multi-step plans, hardware confirmation in the loop, stateless
    /// history pairing, and cancellation.
    /// </summary>
    public class AcceptanceLoopTests
    {
        // ---- scripted client ----
        private sealed class ScriptedClient : IClaudeClient
        {
            private readonly Queue<ClaudeResponse> _turns;
            public ScriptedClient(params ClaudeResponse[] turns) => _turns = new Queue<ClaudeResponse>(turns);
            public ClaudeClientOptions Options { get; } = new ClaudeClientOptions { Model = "claude-opus-4-8", MaxTokens = 4096 };
            public Task<ClaudeResponse> CreateMessageAsync(ClaudeRequest request, CancellationToken ct = default) => Task.FromResult(Next());
            public Task<ClaudeResponse> CreateMessageStreamingAsync(ClaudeRequest request, Action<string> onTextDelta, CancellationToken ct = default) => Task.FromResult(Next());
            private ClaudeResponse Next() => _turns.Count > 1 ? _turns.Dequeue() : _turns.Peek();
        }

        private static ClaudeResponse Say(string text) =>
            new ClaudeResponse { StopReason = "end_turn", Content = { ContentBlock.OfText(text) } };
        private static ClaudeResponse Use(string id, string name, JObject input = null) =>
            new ClaudeResponse { StopReason = "tool_use", Content = { ContentBlock.OfToolUse(id, name, input ?? new JObject()) } };

        // ---- fake hosts ----
        private sealed class FakeReadHost : IAssistantReadHost
        {
            public int AppStateCalls;
            public AppStateSnapshot GetAppState() { AppStateCalls++; return new AppStateSnapshot { PersonalityName = "CW", Connected = false, PipelineStage = "idle" }; }
            public IReadOnlyList<PersonalityInfo> ListPersonalities() => new List<PersonalityInfo>();
            public JObject GetCurrentConfig() => new JObject();
            public IReadOnlyList<ValidationResult> GetValidation() => new List<ValidationResult>();
            public ReadoutSnapshot GetReadout() => null;
        }

        private sealed class FakeConfigureHost : IAssistantConfigureHost
        {
            public readonly List<string> Calls = new List<string>();
            public JObject SetSourcePersonality(string name) { Calls.Add("personality:" + name); return new JObject { ["personality"] = name }; }
            public JObject Configure(string personality, JObject args) { Calls.Add("configure:" + personality); return new JObject(); }
            public JObject SelectPlotView(string pane, string view) { Calls.Add("plot"); return new JObject(); }
            public JObject SetProject(string action, string path) { Calls.Add("project:" + action); return new JObject(); }
            public JObject CalculateWaveform() { Calls.Add("calculate"); return new JObject { ["sample_count"] = 4096, ["summary"] = "4096 samples." }; }
        }

        private sealed class FakeHardwareHost : IAssistantHardwareHost
        {
            public readonly List<string> Calls = new List<string>();
            public JObject ConnectInstrument(string resource) { Calls.Add("connect"); return new JObject(); }
            public JObject DisconnectInstrument() { Calls.Add("disconnect"); return new JObject(); }
            public JObject DownloadWaveform() { Calls.Add("download"); return new JObject { ["bytes"] = 16384 }; }
            public JObject PlayRf() { Calls.Add("play"); return new JObject(); }
            public JObject StopRf() { Calls.Add("stop"); return new JObject(); }
            public JObject SetInstrumentSettings(JObject args) { Calls.Add("settings"); return new JObject(); }
        }

        private sealed class CancellingHardwareHost : IAssistantHardwareHost
        {
            public JObject ConnectInstrument(string resource) => new JObject();
            public JObject DisconnectInstrument() => new JObject();
            public JObject DownloadWaveform() => throw new OperationCanceledException();
            public JObject PlayRf() => new JObject();
            public JObject StopRf() => new JObject();
            public JObject SetInstrumentSettings(JObject args) => new JObject();
        }

        private sealed class NoErrorsGate : IValidationGateHost
        {
            public IReadOnlyList<ValidationResult> RevalidateForHardware() => new List<ValidationResult>();
        }

        private sealed class AutoConfirm : IHardwareConfirmer
        {
            public int Calls;
            public Task<bool> ConfirmAsync(ToolConfirmationRequest request, CancellationToken ct) { Calls++; return Task.FromResult(true); }
        }

        private static (AgentLoop loop, ConversationStore store) Build(
            IClaudeClient client, ToolContext ctx, IConfirmationPolicy policy = null, IPreExecutionGate gate = null)
        {
            var registry = new ToolRegistry();
            registry.Register(ReadTools.All());
            registry.Register(ConfigureTools.All());
            registry.Register(HardwareTools.All());
            var dispatcher = new ToolDispatcher(registry, ctx, policy, gate);
            var store = new ConversationStore { SystemPrompt = "sys" };
            var loop = new AgentLoop(client, store, dispatcher, new AgentLoopOptions { Tools = registry.ToToolDefinitions() });
            return (loop, store);
        }

        [Fact]
        public async Task Single_tool_turn_runs_the_read_tool_and_pairs_history()
        {
            var read = new FakeReadHost();
            var ctx = new ToolContext();
            ctx.Register<IAssistantReadHost>(read);

            var client = new ScriptedClient(Use("tu1", "get_app_state"), Say("You are offline, personality CW."));
            (AgentLoop loop, ConversationStore store) = Build(client, ctx);

            ClaudeResponse final = await loop.RunTurnAsync("what's the state?");

            Assert.Equal(1, read.AppStateCalls);
            Assert.Equal("end_turn", final.StopReason);
            // user, assistant(tool_use), user(tool_result), assistant(text)
            Assert.Equal(4, store.Count);
            Assert.Equal(Roles.User, store.Messages[2].Role);
            Assert.Equal("tu1", store.Messages[2].Content[0].ToolUseId);
            Assert.Contains("CW", final.Text());
        }

        [Fact]
        public async Task Multi_step_plan_executes_tools_in_order()
        {
            var cfg = new FakeConfigureHost();
            var ctx = new ToolContext();
            ctx.Register<IAssistantConfigureHost>(cfg);

            var client = new ScriptedClient(
                Use("a", "set_source_personality", new JObject { ["personality"] = "Multitone" }),
                Use("b", "calculate_waveform"),
                Say("Done — 4096 samples."));
            (AgentLoop loop, _) = Build(client, ctx);

            ClaudeResponse final = await loop.RunTurnAsync("make a multitone and calculate it");

            Assert.Equal(new[] { "personality:Multitone", "calculate" }, cfg.Calls.ToArray());
            Assert.Equal("end_turn", final.StopReason);
        }

        [Fact]
        public async Task Hardware_step_in_a_plan_is_confirmed_then_runs()
        {
            var hw = new FakeHardwareHost();
            var ctx = new ToolContext();
            ctx.Register<IAssistantHardwareHost>(hw);
            var confirm = new AutoConfirm();

            var client = new ScriptedClient(Use("d", "download_waveform"), Say("Downloaded."));
            (AgentLoop loop, _) = Build(client, ctx, new EffectConfirmationPolicy(confirm), new ValidationGate(new NoErrorsGate()));

            ClaudeResponse final = await loop.RunTurnAsync("download it");

            Assert.Equal(1, confirm.Calls);
            Assert.Contains("download", hw.Calls);
            Assert.Equal("end_turn", final.StopReason);
        }

        [Fact]
        public async Task Cancellation_during_a_tool_propagates_and_keeps_history_valid()
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantHardwareHost>(new CancellingHardwareHost());

            var client = new ScriptedClient(Use("x", "download_waveform"), Say("never"));
            (AgentLoop loop, ConversationStore store) = Build(client, ctx,
                new EffectConfirmationPolicy(new AutoConfirm()), new ValidationGate(new NoErrorsGate()));

            await Assert.ThrowsAsync<OperationCanceledException>(() => loop.RunTurnAsync("go"));

            ClaudeMessage last = store.Messages[store.Count - 1];
            Assert.Equal(Roles.User, last.Role);
            Assert.True(last.Content[0].IsError);          // tool_result error keeps pairing valid
            Assert.Equal("x", last.Content[0].ToolUseId);
        }
    }
}
