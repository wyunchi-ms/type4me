using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.LLM;

/// <summary>
/// Azure OpenAI SSE streaming LLM client.
/// Uses api-key header auth, deployment-based URL, and no temperature parameter
/// (gpt-5.2-chat does not support temperature).
/// </summary>
public sealed class AzureOpenAIChatClient : ILLMClient
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task WarmUpAsync(string baseURL)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, baseURL);
            req.Headers.ConnectionClose = false;
            await _http.SendAsync(req);
            DebugFileLogger.Log($"[AzureLLM] Connection pre-warmed to {baseURL}");
        }
        catch { /* ignore */ }
    }

    public async Task<string> ProcessAsync(string text, string prompt, LLMConfig config, CancellationToken ct = default)
    {
        var trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText)) return text;

        var finalPrompt = prompt.Replace("{text}", trimmedText);

        // Reconstruct the Azure URL from LLMConfig fields:
        // config.BaseURL = endpoint, config.Model = deploymentName
        // We need apiVersion from credentials — store it via a convention or use default.
        // The AzureOpenAILLMConfig stores apiVersion in credentials but LLMConfig doesn't carry it.
        // We'll use the default api-version here; the full URL is built by the registry caller.
        var url = BuildUrl(config.BaseURL, config.Model, config.ApiVersion ?? "2025-04-01-preview");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", config.ApiKey);
        request.Headers.Add("Accept", "text/event-stream");

        // gpt-5.2-chat: no temperature, no thinking parameter
        var body = new
        {
            messages = new[] { new { role = "user", content = finalPrompt } },
            stream = true,
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        DebugFileLogger.Log($"[AzureLLM] Request: {text.Length} chars, deployment={config.Model}");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            DebugFileLogger.Log($"[AzureLLM] HTTP {(int)response.StatusCode}: {errorBody}");
            throw new HttpRequestException($"Azure OpenAI request failed with status {(int)response.StatusCode}");
        }

        // Parse SSE stream — same format as OpenAI
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var result = new StringBuilder();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ")) continue;
            var payload = line[6..];
            if (payload == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var content))
                    {
                        var chunk = content.GetString();
                        if (chunk != null) result.Append(chunk);
                    }
                }
            }
            catch { /* skip malformed chunks */ }
        }

        DebugFileLogger.Log($"[AzureLLM] Result: {result.Length} chars");
        return result.ToString();
    }

    /// <summary>
    /// Build the Azure OpenAI chat completions URL.
    /// endpoint/openai/deployments/{deployment}/chat/completions?api-version=...
    /// </summary>
    private static string BuildUrl(string endpoint, string deploymentName, string apiVersion = "2025-04-01-preview")
    {
        return $"{endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";
    }
}
