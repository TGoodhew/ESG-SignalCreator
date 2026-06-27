using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;

namespace EsgSignalCreator.Assistant.Agent
{
    /// <summary>
    /// Seam between the agent loop and the tool surface (#80/#83 implement it): given a tool_use block,
    /// validate its arguments, apply the guardrail/confirmation policy, execute it, and return the
    /// matching tool_result block (same tool_use_id; <c>is_error</c> set on failure). The loop only
    /// orchestrates rounds — it does not know what any tool does.
    /// </summary>
    public interface IToolInvoker
    {
        Task<ContentBlock> InvokeAsync(ContentBlock toolUse, CancellationToken ct);
    }
}
