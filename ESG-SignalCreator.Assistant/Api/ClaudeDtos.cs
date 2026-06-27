using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Api
{
    /// <summary>
    /// DTOs for the Anthropic Messages API (#78). The API is stateless — the caller resends the full
    /// message history each turn (see AgentLoop, #79). Content is always modelled as a list of typed
    /// blocks; a single <see cref="ContentBlock"/> class carries the per-type fields (text / tool_use /
    /// tool_result) discriminated by <see cref="ContentBlock.Type"/>, which keeps Newtonsoft mapping
    /// simple (no custom polymorphic converter).
    /// </summary>
    public static class ContentTypes
    {
        public const string Text = "text";
        public const string ToolUse = "tool_use";
        public const string ToolResult = "tool_result";
    }

    public static class Roles
    {
        public const string User = "user";
        public const string Assistant = "assistant";
    }

    /// <summary>One content block in a message (text, a tool call, or a tool result).</summary>
    public sealed class ContentBlock
    {
        [JsonProperty("type")] public string Type { get; set; }

        // text
        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)] public string Text { get; set; }

        // tool_use
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)] public string Id { get; set; }
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string Name { get; set; }
        [JsonProperty("input", NullValueHandling = NullValueHandling.Ignore)] public JObject Input { get; set; }

        // tool_result
        [JsonProperty("tool_use_id", NullValueHandling = NullValueHandling.Ignore)] public string ToolUseId { get; set; }
        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)] public string Content { get; set; }
        [JsonProperty("is_error", NullValueHandling = NullValueHandling.Ignore)] public bool? IsError { get; set; }

        public static ContentBlock OfText(string text) =>
            new ContentBlock { Type = ContentTypes.Text, Text = text };

        public static ContentBlock OfToolUse(string id, string name, JObject input) =>
            new ContentBlock { Type = ContentTypes.ToolUse, Id = id, Name = name, Input = input ?? new JObject() };

        public static ContentBlock OfToolResult(string toolUseId, string content, bool isError = false) =>
            new ContentBlock { Type = ContentTypes.ToolResult, ToolUseId = toolUseId, Content = content, IsError = isError ? (bool?)true : null };
    }

    /// <summary>A single conversation message (role + content blocks).</summary>
    public sealed class ClaudeMessage
    {
        [JsonProperty("role")] public string Role { get; set; }
        [JsonProperty("content")] public List<ContentBlock> Content { get; set; } = new List<ContentBlock>();

        public static ClaudeMessage User(string text) =>
            new ClaudeMessage { Role = Roles.User, Content = { ContentBlock.OfText(text) } };

        public static ClaudeMessage Assistant(IEnumerable<ContentBlock> blocks) =>
            new ClaudeMessage { Role = Roles.Assistant, Content = new List<ContentBlock>(blocks) };
    }

    /// <summary>A tool exposed to the model: name + description + JSON-schema for its input.</summary>
    public sealed class ToolDefinition
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("input_schema")] public JObject InputSchema { get; set; }
    }

    /// <summary>A /v1/messages request.</summary>
    public sealed class ClaudeRequest
    {
        [JsonProperty("model")] public string Model { get; set; }
        [JsonProperty("max_tokens")] public int MaxTokens { get; set; } = 4096;
        [JsonProperty("system", NullValueHandling = NullValueHandling.Ignore)] public string System { get; set; }
        [JsonProperty("messages")] public List<ClaudeMessage> Messages { get; set; } = new List<ClaudeMessage>();
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)] public List<ToolDefinition> Tools { get; set; }
        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)] public double? Temperature { get; set; }
        [JsonProperty("stream", NullValueHandling = NullValueHandling.Ignore)] public bool? Stream { get; set; }
    }

    /// <summary>Token accounting returned with a response.</summary>
    public sealed class Usage
    {
        [JsonProperty("input_tokens")] public int InputTokens { get; set; }
        [JsonProperty("output_tokens")] public int OutputTokens { get; set; }
    }

    /// <summary>A /v1/messages response.</summary>
    public sealed class ClaudeResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("role")] public string Role { get; set; }
        [JsonProperty("model")] public string Model { get; set; }
        [JsonProperty("content")] public List<ContentBlock> Content { get; set; } = new List<ContentBlock>();
        [JsonProperty("stop_reason")] public string StopReason { get; set; }
        [JsonProperty("stop_sequence")] public string StopSequence { get; set; }
        [JsonProperty("usage")] public Usage Usage { get; set; }

        /// <summary>True when the model wants to call one or more tools this turn.</summary>
        [JsonIgnore] public bool WantsToolUse => StopReason == "tool_use";

        /// <summary>All tool_use blocks in this response (empty if none).</summary>
        public IEnumerable<ContentBlock> ToolUses()
        {
            if (Content == null) yield break;
            foreach (ContentBlock b in Content)
                if (b != null && b.Type == ContentTypes.ToolUse) yield return b;
        }

        /// <summary>Concatenated text of all text blocks.</summary>
        public string Text()
        {
            if (Content == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (ContentBlock b in Content)
                if (b != null && b.Type == ContentTypes.Text && b.Text != null) sb.Append(b.Text);
            return sb.ToString();
        }
    }
}
