using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Agent;
using EsgSignalCreator.Assistant.Api;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>
    /// #89: concurrent execution of read tool_uses (configure/hardware stay serialized in emit order),
    /// emit-order result assembly, and conversation compaction with pairing preserved.
    /// </summary>
    public class ParallelAndCompactionTests
    {
        private sealed class ScriptedClient : IClaudeClient
        {
            private readonly Queue<ClaudeResponse> _turns;
            public ScriptedClient(params ClaudeResponse[] turns) => _turns = new Queue<ClaudeResponse>(turns);
            public ClaudeClientOptions Options { get; } = new ClaudeClientOptions { Model = "m", MaxTokens = 4096 };
            public Task<ClaudeResponse> CreateMessageAsync(ClaudeRequest request, CancellationToken ct = default) => Task.FromResult(Next());
            public Task<ClaudeResponse> CreateMessageStreamingAsync(ClaudeRequest request, Action<string> onTextDelta, CancellationToken ct = default) => Task.FromResult(Next());
            private ClaudeResponse Next() => _turns.Count > 1 ? _turns.Dequeue() : _turns.Peek();
        }

        /// <summary>Tracks peak concurrency and the order tools were entered.</summary>
        private sealed class ConcurrencyInvoker : IToolInvoker
        {
            private readonly object _lock = new object();
            public int Active;
            public int MaxConcurrent;
            public readonly List<string> Entered = new List<string>();

            public async Task<ContentBlock> InvokeAsync(ContentBlock toolUse, CancellationToken ct)
            {
                lock (_lock) { Active++; if (Active > MaxConcurrent) MaxConcurrent = Active; Entered.Add(toolUse.Id); }
                await Task.Delay(60, ct).ConfigureAwait(false);
                lock (_lock) { Active--; }
                return ContentBlock.OfToolResult(toolUse.Id, "{\"ok\":true}");
            }
        }

        private static ClaudeResponse TwoTools(string n1, string n2) => new ClaudeResponse
        {
            StopReason = "tool_use",
            Content = { ContentBlock.OfToolUse("r1", n1, new Newtonsoft.Json.Linq.JObject()), ContentBlock.OfToolUse("r2", n2, new Newtonsoft.Json.Linq.JObject()) }
        };
        private static ClaudeResponse End() => new ClaudeResponse { StopReason = "end_turn", Content = { ContentBlock.OfText("done") } };

        private static (AgentLoop loop, ConversationStore store) Build(IClaudeClient client, IToolInvoker invoker, AgentLoopOptions opts)
        {
            var store = new ConversationStore { SystemPrompt = "sys" };
            return (new AgentLoop(client, store, invoker, opts), store);
        }

        [Fact]
        public async Task Read_tools_run_concurrently()
        {
            var inv = new ConcurrencyInvoker();
            var client = new ScriptedClient(TwoTools("get_app_state", "get_results_readout"), End());
            (AgentLoop loop, _) = Build(client, inv, new AgentLoopOptions { ReadOnlyClassifier = _ => true });

            await loop.RunTurnAsync("read two things");

            Assert.Equal(2, inv.MaxConcurrent); // both reads overlapped
        }

        [Fact]
        public async Task Non_read_tools_are_serialized_in_emit_order()
        {
            var inv = new ConcurrencyInvoker();
            var client = new ScriptedClient(TwoTools("configure_cw", "calculate_waveform"), End());
            (AgentLoop loop, ConversationStore store) = Build(client, inv, new AgentLoopOptions { ReadOnlyClassifier = _ => false });

            await loop.RunTurnAsync("configure then calc");

            Assert.Equal(1, inv.MaxConcurrent);                       // never overlapped
            Assert.Equal(new[] { "r1", "r2" }, inv.Entered.ToArray()); // emit order
        }

        [Fact]
        public async Task Without_a_classifier_everything_is_sequential()
        {
            var inv = new ConcurrencyInvoker();
            var client = new ScriptedClient(TwoTools("get_app_state", "get_app_state"), End());
            (AgentLoop loop, _) = Build(client, inv, new AgentLoopOptions()); // no classifier

            await loop.RunTurnAsync("x");

            Assert.Equal(1, inv.MaxConcurrent);
        }

        [Fact]
        public async Task Tool_results_are_assembled_in_emit_order()
        {
            var inv = new ConcurrencyInvoker();
            var client = new ScriptedClient(TwoTools("get_app_state", "get_results_readout"), End());
            (AgentLoop loop, ConversationStore store) = Build(client, inv, new AgentLoopOptions { ReadOnlyClassifier = _ => true });

            await loop.RunTurnAsync("x");

            ClaudeMessage toolResults = store.Messages[2]; // user, assistant(tool_use x2), user(tool_result x2)
            Assert.Equal("r1", toolResults.Content[0].ToolUseId);
            Assert.Equal("r2", toolResults.Content[1].ToolUseId);
        }

        // ---- compaction ----

        [Fact]
        public void Compact_trims_oldest_to_the_cap()
        {
            var store = new ConversationStore();
            for (int i = 0; i < 10; i++) store.AddUser("m" + i);
            int removed = store.Compact(4);
            Assert.Equal(6, removed);
            Assert.Equal(4, store.Count);
            Assert.Equal("m6", store.Messages[0].Content[0].Text); // oldest kept
        }

        [Fact]
        public void Compact_does_not_leave_a_leading_orphan_tool_result()
        {
            var store = new ConversationStore();
            store.AddUser("hi");                                                  // 0
            store.Add(ClaudeMessage.Assistant(new[] { ContentBlock.OfToolUse("t", "get_app_state", new Newtonsoft.Json.Linq.JObject()) })); // 1
            store.Add(new ClaudeMessage { Role = Roles.User, Content = { ContentBlock.OfToolResult("t", "{}") } });                          // 2 (orphan if it leads)
            store.Add(ClaudeMessage.Assistant(new[] { ContentBlock.OfText("answer") }));                                                    // 3

            // Cap of 2 would keep messages [2,3] — but [2] is an orphan tool_result, so it must be dropped too.
            store.Compact(2);

            Assert.True(store.Count <= 2);
            Assert.False(store.Messages[0].Role == Roles.User &&
                         store.Messages[0].Content.Any(b => b.Type == ContentTypes.ToolResult));
        }

        [Fact]
        public void Compact_is_a_noop_below_the_cap()
        {
            var store = new ConversationStore();
            store.AddUser("a"); store.AddUser("b");
            Assert.Equal(0, store.Compact(5));
            Assert.Equal(0, store.Compact(0)); // 0 = unlimited
            Assert.Equal(2, store.Count);
        }
    }
}
