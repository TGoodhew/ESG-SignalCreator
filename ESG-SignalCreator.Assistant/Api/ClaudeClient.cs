using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EsgSignalCreator.Assistant.Api
{
    /// <summary>Current Claude model IDs used by the assistant (confirm at build time).</summary>
    public static class ClaudeModels
    {
        /// <summary>Capable default for planning / tool use.</summary>
        public const string DefaultPlanning = "claude-opus-4-8";

        /// <summary>Faster, cheaper model for simple turns.</summary>
        public const string FastTurns = "claude-haiku-4-5";
    }

    /// <summary>Configuration for <see cref="ClaudeClient"/>.</summary>
    public sealed class ClaudeClientOptions
    {
        public string ApiKey { get; set; }
        public string Model { get; set; } = ClaudeModels.DefaultPlanning;
        public string FastModel { get; set; } = ClaudeModels.FastTurns;
        public int MaxTokens { get; set; } = 4096;
        public string AnthropicVersion { get; set; } = "2023-06-01";
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        public string AnthropicBeta { get; set; }

        /// <summary>Retries for transient (network / 408 / 429 / 5xx / 529) failures.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Base backoff in ms; attempt N waits BaseRetryDelayMs * 2^(N-1) (or Retry-After).</summary>
        public int BaseRetryDelayMs { get; set; } = 500;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
    }

    /// <summary>An error from the Messages API (non-2xx) or transport.</summary>
    public sealed class ClaudeApiException : Exception
    {
        public ClaudeApiException(string message, int? statusCode = null, string responseBody = null, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public int? StatusCode { get; }
        public string ResponseBody { get; }
    }

    /// <summary>
    /// Thin client for the Anthropic Messages API (<c>/v1/messages</c>) (#78). Enables TLS 1.2 (needed
    /// on .NET Framework 4.7.2), builds requests, retries transient failures with exponential backoff,
    /// and supports both buffered and streaming responses. Accepts an injected <see cref="HttpClient"/>
    /// so it is unit-testable against a fake handler.
    /// </summary>
    public sealed class ClaudeClient : IDisposable
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;
        private readonly ClaudeClientOptions _options;
        private readonly bool _ownsHttp;

        public ClaudeClient(ClaudeClientOptions options)
            : this(CreateDefaultHttpClient(options), options, ownsHttp: true) { }

        public ClaudeClient(HttpClient http, ClaudeClientOptions options, bool ownsHttp = false)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _ownsHttp = ownsHttp;
        }

        public ClaudeClientOptions Options => _options;

        private static HttpClient CreateDefaultHttpClient(ClaudeClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            // .NET Framework 4.7.2 does not negotiate TLS 1.2 by default on all configs.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { BaseAddress = new Uri(options.BaseUrl), Timeout = options.Timeout };
        }

        /// <summary>Serialize a request to the wire JSON (nulls omitted). Exposed for testing/inspection.</summary>
        public static string Serialize(ClaudeRequest request) =>
            JsonConvert.SerializeObject(request, JsonSettings);

        /// <summary>POST a buffered (non-streaming) message request and return the parsed response.</summary>
        public async Task<ClaudeResponse> CreateMessageAsync(ClaudeRequest request, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Stream = null;
            string json = Serialize(request);

            using (HttpResponseMessage resp = await SendWithRetryAsync(() => BuildPost(json), HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
            {
                string body = resp.Content != null ? await resp.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
                if (!resp.IsSuccessStatusCode)
                    throw new ClaudeApiException("Messages API returned " + (int)resp.StatusCode + " " + resp.StatusCode + ".", (int)resp.StatusCode, body);
                return JsonConvert.DeserializeObject<ClaudeResponse>(body);
            }
        }

        /// <summary>
        /// POST a streaming message request; <paramref name="onTextDelta"/> fires for each text delta as
        /// it arrives. Returns the fully-assembled response (text + any tool_use blocks + stop_reason).
        /// </summary>
        public async Task<ClaudeResponse> CreateMessageStreamingAsync(ClaudeRequest request, Action<string> onTextDelta, CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Stream = true;
            string json = Serialize(request);

            using (HttpResponseMessage resp = await SendWithRetryAsync(() => BuildPost(json), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    string body = resp.Content != null ? await resp.Content.ReadAsStringAsync().ConfigureAwait(false) : string.Empty;
                    throw new ClaudeApiException("Messages API (stream) returned " + (int)resp.StatusCode + " " + resp.StatusCode + ".", (int)resp.StatusCode, body);
                }

                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var reader = new System.IO.StreamReader(stream, Encoding.UTF8))
                {
                    return SseParser.Parse(reader, onTextDelta, ct);
                }
            }
        }

        private HttpRequestMessage BuildPost(string json)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey ?? string.Empty);
            req.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);
            if (!string.IsNullOrEmpty(_options.AnthropicBeta))
                req.Headers.TryAddWithoutValidation("anthropic-beta", _options.AnthropicBeta);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return req;
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> factory, HttpCompletionOption completion, CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                HttpResponseMessage resp;
                try
                {
                    resp = await _http.SendAsync(factory(), completion, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // user cancellation — do not retry
                }
                catch (Exception) when (attempt <= _options.MaxRetries)
                {
                    await DelayAsync(attempt, null, ct).ConfigureAwait(false);
                    continue;
                }
                catch (Exception ex)
                {
                    throw new ClaudeApiException("Network error calling the Messages API: " + ex.Message, null, null, ex);
                }

                if (IsTransient((int)resp.StatusCode) && attempt <= _options.MaxRetries)
                {
                    RetryConditionHeaderValue retryAfter = resp.Headers.RetryAfter;
                    resp.Dispose();
                    await DelayAsync(attempt, retryAfter, ct).ConfigureAwait(false);
                    continue;
                }

                return resp;
            }
        }

        private async Task DelayAsync(int attempt, RetryConditionHeaderValue retryAfter, CancellationToken ct)
        {
            TimeSpan delay;
            if (retryAfter != null && retryAfter.Delta.HasValue)
                delay = retryAfter.Delta.Value;
            else
                delay = TimeSpan.FromMilliseconds(_options.BaseRetryDelayMs * Math.Pow(2, attempt - 1));
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        /// <summary>HTTP status codes worth retrying.</summary>
        public static bool IsTransient(int status) =>
            status == 408 || status == 429 || status == 500 || status == 502 ||
            status == 503 || status == 504 || status == 529;

        public void Dispose()
        {
            if (_ownsHttp) _http.Dispose();
        }
    }
}
