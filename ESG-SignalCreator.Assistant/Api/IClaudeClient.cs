using System;
using System.Threading;
using System.Threading.Tasks;

namespace EsgSignalCreator.Assistant.Api
{
    /// <summary>
    /// Abstraction over <see cref="ClaudeClient"/> so the agent loop (and its tests) depend on an
    /// interface rather than the concrete HTTP client.
    /// </summary>
    public interface IClaudeClient
    {
        ClaudeClientOptions Options { get; }

        Task<ClaudeResponse> CreateMessageAsync(ClaudeRequest request, CancellationToken ct = default);

        Task<ClaudeResponse> CreateMessageStreamingAsync(ClaudeRequest request, Action<string> onTextDelta, CancellationToken ct = default);
    }
}
