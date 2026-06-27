using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class ClaudeClientTests
    {
        /// <summary>Fake handler returning a queued sequence of responses, recording attempts.</summary>
        private sealed class QueueHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _responses;
            public int Calls { get; private set; }
            public string LastBody { get; private set; }

            public QueueHandler(params Func<HttpResponseMessage>[] responses)
            {
                _responses = new Queue<Func<HttpResponseMessage>>(responses);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                LastBody = request.Content != null ? await request.Content.ReadAsStringAsync() : null;
                Func<HttpResponseMessage> next = _responses.Count > 1 ? _responses.Dequeue() : _responses.Peek();
                return next();
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
            new HttpResponseMessage(code) { Content = new StringContent(body) };

        private static ClaudeClient ClientFor(HttpMessageHandler handler, out QueueHandler q, int maxRetries = 3)
        {
            q = handler as QueueHandler;
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
            var opts = new ClaudeClientOptions { ApiKey = "sk-test", BaseRetryDelayMs = 0, MaxRetries = maxRetries };
            return new ClaudeClient(http, opts, ownsHttp: true);
        }

        private const string OkResponse =
            "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-opus-4-8\"," +
            "\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}],\"stop_reason\":\"end_turn\"," +
            "\"usage\":{\"input_tokens\":10,\"output_tokens\":3}}";

        [Fact]
        public void Serialize_emits_expected_wire_shape()
        {
            var req = new ClaudeRequest
            {
                Model = ClaudeModels.DefaultPlanning,
                System = "be helpful",
                Messages =
                {
                    ClaudeMessage.User("hi"),
                    ClaudeMessage.Assistant(new[] { ContentBlock.OfToolUse("tu_1", "get_state", new JObject()) }),
                    new ClaudeMessage { Role = Roles.User, Content = { ContentBlock.OfToolResult("tu_1", "{\"ok\":true}") } }
                },
                Tools = new List<ToolDefinition>
                {
                    new ToolDefinition { Name = "get_state", Description = "read", InputSchema = JObject.Parse("{\"type\":\"object\"}") }
                }
            };

            string json = ClaudeClient.Serialize(req);
            JObject o = JObject.Parse(json);

            Assert.Equal("claude-opus-4-8", (string)o["model"]);
            Assert.Equal(4096, (int)o["max_tokens"]);
            Assert.Equal("be helpful", (string)o["system"]);
            Assert.Equal("tu_1", (string)o["messages"][1]["content"][0]["id"]);
            Assert.Equal("tu_1", (string)o["messages"][2]["content"][0]["tool_use_id"]);
            Assert.Equal("get_state", (string)o["tools"][0]["name"]);
            Assert.NotNull(o["tools"][0]["input_schema"]);
            Assert.Null(o["stream"]); // nulls omitted
        }

        [Fact]
        public async Task CreateMessage_parses_a_successful_response()
        {
            ClaudeClient c = ClientFor(new QueueHandler(() => Json(HttpStatusCode.OK, OkResponse)), out _);
            ClaudeResponse r = await c.CreateMessageAsync(new ClaudeRequest { Model = ClaudeModels.DefaultPlanning, Messages = { ClaudeMessage.User("hi") } });

            Assert.Equal("end_turn", r.StopReason);
            Assert.False(r.WantsToolUse);
            Assert.Equal("Hello", r.Text());
            Assert.Equal(3, r.Usage.OutputTokens);
        }

        [Fact]
        public async Task CreateMessage_exposes_tool_use_blocks()
        {
            const string toolResp =
                "{\"id\":\"msg_2\",\"role\":\"assistant\",\"content\":[" +
                "{\"type\":\"text\",\"text\":\"working\"}," +
                "{\"type\":\"tool_use\",\"id\":\"tu_9\",\"name\":\"set_cw\",\"input\":{\"freq_hz\":1000000000}}]," +
                "\"stop_reason\":\"tool_use\",\"usage\":{\"input_tokens\":5,\"output_tokens\":7}}";
            ClaudeClient c = ClientFor(new QueueHandler(() => Json(HttpStatusCode.OK, toolResp)), out _);

            ClaudeResponse r = await c.CreateMessageAsync(new ClaudeRequest { Messages = { ClaudeMessage.User("set a tone") } });

            Assert.True(r.WantsToolUse);
            ContentBlock tu = r.ToolUses().Single();
            Assert.Equal("set_cw", tu.Name);
            Assert.Equal(1000000000L, (long)tu.Input["freq_hz"]);
        }

        [Fact]
        public async Task Transient_then_success_is_retried()
        {
            var handler = new QueueHandler(
                () => Json((HttpStatusCode)529, "{\"error\":\"overloaded\"}"),
                () => Json(HttpStatusCode.OK, OkResponse));
            ClaudeClient c = ClientFor(handler, out QueueHandler q);

            ClaudeResponse r = await c.CreateMessageAsync(new ClaudeRequest { Messages = { ClaudeMessage.User("hi") } });

            Assert.Equal("Hello", r.Text());
            Assert.Equal(2, q.Calls); // one retry
        }

        [Fact]
        public async Task Non_transient_error_throws_without_retry()
        {
            var handler = new QueueHandler(() => Json(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}"));
            ClaudeClient c = ClientFor(handler, out QueueHandler q);

            ClaudeApiException ex = await Assert.ThrowsAsync<ClaudeApiException>(
                () => c.CreateMessageAsync(new ClaudeRequest { Messages = { ClaudeMessage.User("hi") } }));

            Assert.Equal(400, ex.StatusCode);
            Assert.Equal(1, q.Calls);
        }

        [Fact]
        public async Task Retries_are_capped_then_surface_the_error()
        {
            var handler = new QueueHandler(() => Json((HttpStatusCode)503, "{\"error\":\"down\"}"));
            ClaudeClient c = ClientFor(handler, out QueueHandler q, maxRetries: 2);

            await Assert.ThrowsAsync<ClaudeApiException>(
                () => c.CreateMessageAsync(new ClaudeRequest { Messages = { ClaudeMessage.User("hi") } }));

            Assert.Equal(3, q.Calls); // initial + 2 retries
        }

        [Fact]
        public void IsTransient_classifies_status_codes()
        {
            Assert.True(ClaudeClient.IsTransient(429));
            Assert.True(ClaudeClient.IsTransient(529));
            Assert.True(ClaudeClient.IsTransient(503));
            Assert.False(ClaudeClient.IsTransient(400));
            Assert.False(ClaudeClient.IsTransient(200));
        }
    }
}
