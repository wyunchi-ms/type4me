using Type4Me.Models;

namespace Type4Me.LLM;

/// <summary>
/// Common interface for LLM clients (OpenAI-compatible and Claude).
/// </summary>
public interface ILLMClient
{
    /// <summary>Process text through LLM with the given prompt template.</summary>
    Task<string> ProcessAsync(string text, string prompt, LLMConfig config, CancellationToken ct = default);

    /// <summary>Pre-establish TCP+TLS connection for faster first request.</summary>
    Task WarmUpAsync(string baseURL);
}
