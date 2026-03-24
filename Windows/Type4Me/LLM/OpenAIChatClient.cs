using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.LLM;

/// <summary>
/// OpenAI-compatible SSE streaming LLM client.
/// Works with Doubao, MiniMax, Bailian, Kimi, OpenRouter, OpenAI, Gemini, DeepSeek, Zhipu.
/// </summary>
public sealed class OpenAIChatClient : ILLMClient
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task WarmUpAsync(string baseURL)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, baseURL);
            req.Headers.ConnectionClose = false;
            await _http.SendAsync(req);
            DebugFileLogger.Log($"[LLM] Connection pre-warmed to {baseURL}");
        }
        catch { /* ignore */ }
    }

    public async Task<string> ProcessAsync(string text, string prompt, LLMConfig config, CancellationToken ct = default)
    {
        var trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText)) return text;

        var finalPrompt = prompt.Replace("{text}", trimmedText);
        var url = $"{config.BaseURL.TrimEnd('/')}/chat/completions";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        request.Headers.Add("Accept", "text/event-stream");

        var body = new
        {
            model = config.Model,
            messages = new[] { new { role = "user", content = finalPrompt } },
            stream = true,
            thinking = new { type = "disabled" },
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        DebugFileLogger.Log($"[LLM] Request: {text.Length} chars, endpoint={config.Model}");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            DebugFileLogger.Log($"[LLM] HTTP {(int)response.StatusCode}");
            throw new HttpRequestException($"LLM request failed with status {(int)response.StatusCode}");
        }

        // Parse SSE stream
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
                        var text2 = content.GetString();
                        if (text2 != null) result.Append(text2);
                    }
                }
            }
            catch { /* skip malformed chunks */ }
        }

        DebugFileLogger.Log($"[LLM] Result: {result.Length} chars");
        return result.ToString();
    }
}
