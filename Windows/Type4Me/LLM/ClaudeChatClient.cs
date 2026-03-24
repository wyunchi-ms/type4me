using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.LLM;

/// <summary>
/// Anthropic Messages API streaming LLM client.
/// </summary>
public sealed class ClaudeChatClient : ILLMClient
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task WarmUpAsync(string baseURL)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, baseURL);
            await _http.SendAsync(req);
            DebugFileLogger.Log($"[Claude] Connection pre-warmed to {baseURL}");
        }
        catch { /* ignore */ }
    }

    public async Task<string> ProcessAsync(string text, string prompt, LLMConfig config, CancellationToken ct = default)
    {
        var trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText)) return text;

        var finalPrompt = prompt.Replace("{text}", trimmedText);
        var url = $"{config.BaseURL.TrimEnd('/')}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Add("Accept", "text/event-stream");

        var body = new
        {
            model = config.Model,
            max_tokens = 4096,
            messages = new[] { new { role = "user", content = finalPrompt } },
            stream = true,
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        DebugFileLogger.Log($"[Claude] Request: {text.Length} chars, model={config.Model}");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            DebugFileLogger.Log($"[Claude] HTTP {(int)response.StatusCode}");
            throw new HttpRequestException($"Claude request failed with status {(int)response.StatusCode}");
        }

        // Parse SSE stream (Anthropic format)
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var result = new StringBuilder();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data: ")) continue;
            var payload = line[6..];

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                switch (type)
                {
                    case "content_block_delta":
                        if (root.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("text", out var textEl))
                        {
                            var chunk = textEl.GetString();
                            if (chunk != null) result.Append(chunk);
                        }
                        break;

                    case "message_stop":
                        goto done;
                }
            }
            catch { /* skip malformed chunks */ }
        }

        done:
        DebugFileLogger.Log($"[Claude] Result: {result.Length} chars");
        return result.ToString();
    }
}
