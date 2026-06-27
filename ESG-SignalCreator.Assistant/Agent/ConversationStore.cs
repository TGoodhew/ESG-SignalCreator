using System.Collections.Generic;
using System.Linq;
using EsgSignalCreator.Assistant.Api;

namespace EsgSignalCreator.Assistant.Agent
{
    /// <summary>
    /// Owns the conversation state (#79). The Messages API is stateless, so the app keeps the full
    /// history here and resends it each turn. Also enforces tool_use ↔ tool_result pairing, which the
    /// API requires: every assistant turn that emits tool_use blocks must be followed by a user turn
    /// carrying a tool_result for each of them.
    /// </summary>
    public sealed class ConversationStore
    {
        private readonly List<ClaudeMessage> _messages = new List<ClaudeMessage>();

        /// <summary>System prompt sent with every request (set by the host).</summary>
        public string SystemPrompt { get; set; }

        public IReadOnlyList<ClaudeMessage> Messages => _messages;
        public int Count => _messages.Count;

        public void AddUser(string text) => _messages.Add(ClaudeMessage.User(text));
        public void Add(ClaudeMessage message) => _messages.Add(message);
        public void Clear() => _messages.Clear();

        /// <summary>A copy of the history for sending (so the live list can't mutate mid-request).</summary>
        public List<ClaudeMessage> Snapshot() => new List<ClaudeMessage>(_messages);

        /// <summary>
        /// Trim the oldest messages so at most <paramref name="maxMessages"/> remain (#89 conversation
        /// compaction). Keeps pairing valid: after trimming it never leaves a leading user turn whose
        /// content is orphaned tool_result blocks (whose assistant tool_use was trimmed). The system
        /// prompt lives outside the message list, so it is unaffected. Returns the number removed.
        /// </summary>
        public int Compact(int maxMessages)
        {
            if (maxMessages <= 0 || _messages.Count <= maxMessages) return 0;

            int removed = _messages.Count - maxMessages;
            _messages.RemoveRange(0, removed);

            // Don't start the history with orphaned tool_result blocks.
            while (_messages.Count > 0 && IsToolResultMessage(_messages[0]))
            {
                _messages.RemoveAt(0);
                removed++;
            }
            return removed;
        }

        private static bool IsToolResultMessage(ClaudeMessage m) =>
            m.Role == Roles.User && m.Content != null && m.Content.Any(b => b != null && b.Type == ContentTypes.ToolResult);

        /// <summary>
        /// If the last message is an assistant turn with unanswered tool_use blocks, append a user turn
        /// with an error tool_result for each — so the history stays valid after a cap or cancellation.
        /// Returns true if it added anything.
        /// </summary>
        public bool EnsureToolResultsPaired(string reason)
        {
            if (_messages.Count == 0) return false;
            ClaudeMessage last = _messages[_messages.Count - 1];
            if (last.Role != Roles.Assistant) return false;

            List<ContentBlock> pending = last.Content.Where(b => b != null && b.Type == ContentTypes.ToolUse).ToList();
            if (pending.Count == 0) return false;

            var results = pending.Select(tu => ContentBlock.OfToolResult(tu.Id, reason, isError: true)).ToList();
            _messages.Add(new ClaudeMessage { Role = Roles.User, Content = results });
            return true;
        }
    }
}
