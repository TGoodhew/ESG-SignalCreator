using System.Collections.Generic;
using System.IO;
using System.Linq;
using EsgSignalCreator.Assistant.Api;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class SseParserTests
    {
        private const string TextStream =
            "event: message_start\n" +
            "data: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_1\",\"model\":\"claude-opus-4-8\",\"usage\":{\"input_tokens\":12,\"output_tokens\":0}}}\n" +
            "\n" +
            "event: content_block_start\n" +
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n" +
            "\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hel\"}}\n" +
            "\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"lo\"}}\n" +
            "\n" +
            "event: content_block_stop\n" +
            "data: {\"type\":\"content_block_stop\",\"index\":0}\n" +
            "\n" +
            "event: message_delta\n" +
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":5}}\n" +
            "\n" +
            "event: message_stop\n" +
            "data: {\"type\":\"message_stop\"}\n";

        [Fact]
        public void Streams_text_deltas_and_assembles_the_response()
        {
            var deltas = new List<string>();
            ClaudeResponse r = SseParser.Parse(new StringReader(TextStream), deltas.Add);

            Assert.Equal(new[] { "Hel", "lo" }, deltas.ToArray());
            Assert.Equal("Hello", r.Text());
            Assert.Equal("end_turn", r.StopReason);
            Assert.Equal("msg_1", r.Id);
            Assert.Equal(12, r.Usage.InputTokens);
            Assert.Equal(5, r.Usage.OutputTokens);
        }

        [Fact]
        public void Assembles_tool_use_from_input_json_deltas()
        {
            const string toolStream =
                "data: {\"type\":\"message_start\",\"message\":{\"id\":\"m\",\"model\":\"x\",\"usage\":{\"input_tokens\":1,\"output_tokens\":0}}}\n" +
                "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"id\":\"tu_1\",\"name\":\"set_cw\"}}\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"freq_hz\\\":\"}}\n" +
                "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"1000000000}\"}}\n" +
                "data: {\"type\":\"content_block_stop\",\"index\":0}\n" +
                "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"tool_use\"},\"usage\":{\"output_tokens\":9}}\n" +
                "data: {\"type\":\"message_stop\"}\n";

            ClaudeResponse r = SseParser.Parse(new StringReader(toolStream), null);

            Assert.True(r.WantsToolUse);
            ContentBlock tu = r.ToolUses().Single();
            Assert.Equal("set_cw", tu.Name);
            Assert.Equal("tu_1", tu.Id);
            Assert.Equal(1000000000L, (long)tu.Input["freq_hz"]);
        }

        [Fact]
        public void Tolerates_keepalives_and_blank_lines()
        {
            const string noisy =
                ": keep-alive\n" +
                "\n" +
                "data: {\"type\":\"message_start\",\"message\":{\"id\":\"m\",\"model\":\"x\"}}\n" +
                "data: \n" +
                "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n";

            ClaudeResponse r = SseParser.Parse(new StringReader(noisy), null);
            Assert.Equal("end_turn", r.StopReason);
        }
    }
}
