using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Agent;
using EsgSignalCreator.Assistant.Api;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class AgentLoopTests
    {
        private sealed class FakeClient : IClaudeClient
        {
            private readonly Queue<ClaudeResponse> _responses;
            public readonly List<ClaudeRequest> Requests = new List<ClaudeRequest>();
            public bool StreamingUsed;

            public FakeClient(params ClaudeResponse[] responses) => _responses = new Queue<ClaudeResponse>(responses);
            public ClaudeClientOptions Options { get; } = new ClaudeClientOptions { Model = "claude-opus-4-8", MaxTokens = 4096 };

            public Task<ClaudeResponse> CreateMessageAsync(ClaudeRequest request, CancellationToken ct = default)
            {
                Requests.Add(request);
                return Task.FromResult(Next());
            }

            public Task<ClaudeResponse> CreateMessageStreamingAsync(ClaudeRequest request, Action<string> onTextDelta, CancellationToken ct = default)
            {
                StreamingUsed = true;
                Requests.Add(request);
                onTextDelta?.Invoke("hi");
                return Task.FromResult(Next());
            }

            private ClaudeResponse Next() => _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
        }

        private sealed class FakeInvoker : IToolInvoker
        {
            public int Calls;
            public Func<ContentBlock, ContentBlock> Handler;
            public bool ThrowCancel;

            public Task<ContentBlock> InvokeAsync(ContentBlock toolUse, CancellationToken ct)
            {
                Calls++;
                if (ThrowCancel) throw new OperationCanceledException();
                ContentBlock r = Handler != null ? Handler(toolUse) : ContentBlock.OfToolResult(toolUse.Id, "{\"ok\":true}");
                return Task.FromResult(r);
            }
        }

        private static ClaudeResponse Text(string t) =>
            new ClaudeResponse { StopReason = "end_turn", Content = { ContentBlock.OfText(t) } };

        private static ClaudeResponse ToolUse(string id, string name) =>
            new ClaudeResponse { StopReason = "tool_use", Content = { ContentBlock.OfText("working"), ContentBlock.OfToolUse(id, name, new JObject()) } };

        private static AgentLoop Loop(FakeClient c, FakeInvoker inv, out ConversationStore store, AgentLoopOptions opt = null)
        {
            store = new ConversationStore { SystemPrompt = "sys" };
            return new AgentLoop(c, store, inv, opt ?? new AgentLoopOptions());
        }

        [Fact]
        public async Task End_turn_returns_without_tools()
        {
            var c = new FakeClient(Text("hello"));
            var inv = new FakeInvoker();
            AgentLoop loop = Loop(c, inv, out ConversationStore store);

            ClaudeResponse r = await loop.RunTurnAsync("hi");

            Assert.Equal("end_turn", r.StopReason);
            Assert.Equal(0, inv.Calls);
            Assert.Equal(2, store.Count); // user, assistant
            Assert.Single(c.Requests);
        }

        [Fact]
        public async Task Tool_round_executes_then_resends_history()
        {
            var c = new FakeClient(ToolUse("tu_1", "get_state"), Text("done"));
            var inv = new FakeInvoker();
            AgentLoop loop = Loop(c, inv, out ConversationStore store);

            ClaudeResponse r = await loop.RunTurnAsync("read state");

            Assert.Equal("end_turn", r.StopReason);
            Assert.Equal(1, inv.Calls);
            // user, assistant(tool_use), user(tool_result), assistant(text)
            Assert.Equal(4, store.Count);
            Assert.Equal(Roles.User, store.Messages[2].Role);
            Assert.Equal("tu_1", store.Messages[2].Content[0].ToolUseId);
            // Second request resent the full history (3 messages).
            Assert.Equal(2, c.Requests.Count);
            Assert.Equal(3, c.Requests[1].Messages.Count);
            Assert.Equal("sys", c.Requests[1].System);
        }

        [Fact]
        public async Task Round_cap_stops_and_keeps_pairing_valid()
        {
            var c = new FakeClient(ToolUse("tu_x", "loop_tool")); // always wants tools
            var inv = new FakeInvoker();
            AgentLoop loop = Loop(c, inv, out ConversationStore store, new AgentLoopOptions { MaxToolRounds = 2 });

            ClaudeResponse r = await loop.RunTurnAsync("go");

            Assert.Equal("tool_use", r.StopReason);
            Assert.Equal(2, inv.Calls); // rounds 1 and 2 executed; round 3 hit the cap
            ClaudeMessage last = store.Messages[store.Count - 1];
            Assert.Equal(Roles.User, last.Role);
            Assert.True(last.Content[0].IsError);
            Assert.Contains("tool-round limit", last.Content[0].Content);
        }

        [Fact]
        public async Task Cancellation_propagates_and_repairs_pairing()
        {
            var c = new FakeClient(ToolUse("tu_c", "slow"), Text("never"));
            var inv = new FakeInvoker { ThrowCancel = true };
            AgentLoop loop = Loop(c, inv, out ConversationStore store);

            await Assert.ThrowsAsync<OperationCanceledException>(() => loop.RunTurnAsync("go"));

            // The assistant tool_use turn is followed by an error tool_result, so history stays valid.
            ClaudeMessage last = store.Messages[store.Count - 1];
            Assert.Equal(Roles.User, last.Role);
            Assert.Equal("tu_c", last.Content[0].ToolUseId);
            Assert.True(last.Content[0].IsError);
        }

        [Fact]
        public async Task Streaming_path_is_used_when_enabled_with_a_handler()
        {
            var c = new FakeClient(Text("streamed"));
            var inv = new FakeInvoker();
            AgentLoop loop = Loop(c, inv, out _, new AgentLoopOptions { Streaming = true });
            var deltas = new List<string>();
            loop.TextDelta += deltas.Add;

            await loop.RunTurnAsync("hi");

            Assert.True(c.StreamingUsed);
            Assert.Contains("hi", deltas);
        }

        [Fact]
        public async Task A_failing_tool_becomes_an_error_result_not_an_exception()
        {
            var c = new FakeClient(ToolUse("tu_e", "boom"), Text("recovered"));
            var inv = new FakeInvoker { Handler = _ => throw new InvalidOperationException("nope") };
            AgentLoop loop = Loop(c, inv, out ConversationStore store);

            ClaudeResponse r = await loop.RunTurnAsync("go");

            Assert.Equal("end_turn", r.StopReason);
            ClaudeMessage toolResult = store.Messages[2];
            Assert.True(toolResult.Content[0].IsError);
            Assert.Contains("nope", toolResult.Content[0].Content);
        }
    }
}
