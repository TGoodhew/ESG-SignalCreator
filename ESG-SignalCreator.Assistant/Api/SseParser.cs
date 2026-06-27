using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Api
{
    /// <summary>
    /// Parses an Anthropic Messages API server-sent-event (SSE) stream into a <see cref="ClaudeResponse"/>
    /// (#78). Text deltas are surfaced live via a callback; tool_use input JSON is accumulated from
    /// <c>input_json_delta</c> fragments and parsed at block stop. Pure over a <see cref="TextReader"/>,
    /// so it is unit-testable with a canned stream.
    /// </summary>
    public static class SseParser
    {
        public static ClaudeResponse Parse(TextReader reader, Action<string> onTextDelta, CancellationToken ct = default)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var response = new ClaudeResponse { Role = Roles.Assistant, Type = "message", Content = new List<ContentBlock>() };
            var blocks = new SortedDictionary<int, ContentBlock>();
            var toolJson = new Dictionary<int, StringBuilder>();
            int outputTokens = 0;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                ct.ThrowIfCancellationRequested();
                if (!line.StartsWith("data:")) continue; // ignore "event:" lines and blank separators

                string payload = line.Substring("data:".Length).Trim();
                if (payload.Length == 0 || payload == "[DONE]") continue;

                JObject ev;
                try { ev = JObject.Parse(payload); }
                catch { continue; } // tolerate keep-alives / malformed fragments

                string type = (string)ev["type"];
                switch (type)
                {
                    case "message_start":
                        JObject msg = ev["message"] as JObject;
                        if (msg != null)
                        {
                            response.Id = (string)msg["id"] ?? response.Id;
                            response.Model = (string)msg["model"] ?? response.Model;
                            JObject u = msg["usage"] as JObject;
                            if (u != null) response.Usage = new Usage { InputTokens = (int?)u["input_tokens"] ?? 0, OutputTokens = (int?)u["output_tokens"] ?? 0 };
                        }
                        break;

                    case "content_block_start":
                    {
                        int idx = (int?)ev["index"] ?? 0;
                        JObject cb = ev["content_block"] as JObject;
                        var block = new ContentBlock { Type = (string)cb?["type"] };
                        if (block.Type == ContentTypes.Text)
                            block.Text = (string)cb?["text"] ?? string.Empty;
                        else if (block.Type == ContentTypes.ToolUse)
                        {
                            block.Id = (string)cb?["id"];
                            block.Name = (string)cb?["name"];
                            toolJson[idx] = new StringBuilder();
                        }
                        blocks[idx] = block;
                        break;
                    }

                    case "content_block_delta":
                    {
                        int idx = (int?)ev["index"] ?? 0;
                        JObject delta = ev["delta"] as JObject;
                        string dtype = (string)delta?["type"];
                        if (dtype == "text_delta")
                        {
                            string t = (string)delta["text"] ?? string.Empty;
                            if (blocks.TryGetValue(idx, out ContentBlock b)) b.Text = (b.Text ?? string.Empty) + t;
                            if (t.Length > 0) onTextDelta?.Invoke(t);
                        }
                        else if (dtype == "input_json_delta")
                        {
                            string pj = (string)delta["partial_json"] ?? string.Empty;
                            if (toolJson.TryGetValue(idx, out StringBuilder sb)) sb.Append(pj);
                        }
                        break;
                    }

                    case "content_block_stop":
                    {
                        int idx = (int?)ev["index"] ?? 0;
                        if (blocks.TryGetValue(idx, out ContentBlock b) && b.Type == ContentTypes.ToolUse &&
                            toolJson.TryGetValue(idx, out StringBuilder sb))
                        {
                            string raw = sb.ToString();
                            b.Input = raw.Length > 0 ? SafeParse(raw) : new JObject();
                        }
                        break;
                    }

                    case "message_delta":
                    {
                        JObject delta = ev["delta"] as JObject;
                        if (delta != null)
                        {
                            response.StopReason = (string)delta["stop_reason"] ?? response.StopReason;
                            response.StopSequence = (string)delta["stop_sequence"] ?? response.StopSequence;
                        }
                        JObject u = ev["usage"] as JObject;
                        if (u != null) outputTokens = (int?)u["output_tokens"] ?? outputTokens;
                        break;
                    }

                    case "error":
                    {
                        JObject err = ev["error"] as JObject;
                        string m = (string)err?["message"] ?? "stream error";
                        throw new ClaudeApiException("Streaming error: " + m);
                    }

                    case "message_stop":
                        break;
                }
            }

            foreach (KeyValuePair<int, ContentBlock> kv in blocks) response.Content.Add(kv.Value);
            if (response.Usage == null) response.Usage = new Usage();
            if (outputTokens > 0) response.Usage.OutputTokens = outputTokens;
            return response;
        }

        private static JObject SafeParse(string json)
        {
            try { return JObject.Parse(json); }
            catch { return new JObject(); }
        }
    }
}
