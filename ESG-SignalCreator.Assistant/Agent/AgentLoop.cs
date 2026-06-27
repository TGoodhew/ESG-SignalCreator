using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;

namespace EsgSignalCreator.Assistant.Agent
{
    public sealed class AgentLoopOptions
    {
        /// <summary>Maximum tool rounds in one turn before stopping (runaway-loop guard).</summary>
        public int MaxToolRounds { get; set; } = 12;

        /// <summary>Tools advertised to the model (every enabled tool's name/description/schema).</summary>
        public List<ToolDefinition> Tools { get; set; }

        /// <summary>Stream assistant text live (requires a TextDelta handler).</summary>
        public bool Streaming { get; set; }

        /// <summary>Model override; falls back to the client's configured model.</summary>
        public string Model { get; set; }
    }

    /// <summary>
    /// The §5 agentic loop (#79): build request from the full history, call the Messages API, and while
    /// the model wants tools, execute each tool_use via <see cref="IToolInvoker"/>, append the
    /// tool_result blocks, and resend — until <c>end_turn</c> or the round cap. Designed to run off the
    /// UI thread; the supplied <see cref="CancellationToken"/> is the Stop button. tool_use ↔ tool_result
    /// pairing is preserved even on cap/cancel so the conversation stays resumable.
    /// </summary>
    public sealed class AgentLoop
    {
        private readonly IClaudeClient _client;
        private readonly ConversationStore _store;
        private readonly IToolInvoker _invoker;
        private readonly AgentLoopOptions _options;

        /// <summary>Fires for each streamed text delta (only when <see cref="AgentLoopOptions.Streaming"/>).</summary>
        public event Action<string> TextDelta;

        /// <summary>Fires after each completed tool round with the 1-based round number.</summary>
        public event Action<int> ToolRoundCompleted;

        public AgentLoop(IClaudeClient client, ConversationStore store, IToolInvoker invoker, AgentLoopOptions options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
            _options = options ?? new AgentLoopOptions();
        }

        /// <summary>Add the user's message and run the loop to completion (or cap/cancel).</summary>
        public Task<ClaudeResponse> RunTurnAsync(string userText, CancellationToken ct = default)
        {
            _store.AddUser(userText);
            return ContinueAsync(ct);
        }

        private async Task<ClaudeResponse> ContinueAsync(CancellationToken ct)
        {
            int rounds = 0;
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    ClaudeRequest request = BuildRequest();
                    ClaudeResponse response = (_options.Streaming && TextDelta != null)
                        ? await _client.CreateMessageStreamingAsync(request, t => TextDelta?.Invoke(t), ct).ConfigureAwait(false)
                        : await _client.CreateMessageAsync(request, ct).ConfigureAwait(false);

                    _store.Add(ClaudeMessage.Assistant(response.Content));

                    if (!response.WantsToolUse)
                        return response; // end_turn / max_tokens / stop_sequence — terminal

                    rounds++;
                    if (rounds > _options.MaxToolRounds)
                    {
                        _store.EnsureToolResultsPaired("Stopped: reached the tool-round limit (" + _options.MaxToolRounds + ").");
                        return response;
                    }

                    var results = new List<ContentBlock>();
                    foreach (ContentBlock toolUse in response.ToolUses())
                    {
                        ct.ThrowIfCancellationRequested();
                        ContentBlock result;
                        try
                        {
                            result = await _invoker.InvokeAsync(toolUse, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            result = ContentBlock.OfToolResult(toolUse.Id, "Tool '" + toolUse.Name + "' failed: " + ex.Message, isError: true);
                        }
                        results.Add(result);
                    }

                    _store.Add(new ClaudeMessage { Role = Roles.User, Content = results });
                    ToolRoundCompleted?.Invoke(rounds);
                }
            }
            catch (OperationCanceledException)
            {
                // Keep the history valid so the next turn can proceed.
                _store.EnsureToolResultsPaired("Cancelled by the user.");
                throw;
            }
        }

        private ClaudeRequest BuildRequest() => new ClaudeRequest
        {
            Model = _options.Model ?? _client.Options.Model,
            MaxTokens = _client.Options.MaxTokens,
            System = _store.SystemPrompt,
            Messages = _store.Snapshot(),
            Tools = _options.Tools
        };
    }
}
