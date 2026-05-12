using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Type4Me.Services;

/// <summary>
/// Captures HTTP request/response pairs for the in-app debug console.
/// Subscribers (e.g. AppViewModel) get a structured event with redacted headers
/// and truncated bodies. Also forwards a one-line summary to DebugFileLogger.
///
/// Usage: replace `new HttpClient()` with `HttpLogger.CreateClient("Tag")` in any
/// component whose traffic should be visible.
/// </summary>
public static class HttpLogger
{
    private const int MaxBodyChars = 4096;

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-Api-Key",
        "X-Api-Access-Key",
        "X-Api-App-Key",
        "api-key",
        "Ocp-Apim-Subscription-Key",
        "anthropic-api-key",
        "x-goog-api-key",
    };

    /// <summary>
    /// Subscribed by AppViewModel to push entries into DebugLogViewModel.
    /// Args: (tag, requestSummary, responseSummary, isError).
    /// </summary>
    public static event Action<string, string, string, bool>? OnHttpEvent;

    /// <summary>
    /// Build an HttpClient whose traffic is captured. `tag` shows up in the log
    /// (e.g. "Volcano-Flash", "OpenAI-Chat").
    /// </summary>
    public static HttpClient CreateClient(string tag, TimeSpan? timeout = null)
    {
        var handler = new LoggingHandler(tag) { InnerHandler = new HttpClientHandler() };
        var client = new HttpClient(handler);
        if (timeout.HasValue) client.Timeout = timeout.Value;
        return client;
    }

    /// <summary>
    /// Manually log a non-HttpClient call (e.g. WebSocket handshake).
    /// </summary>
    public static void LogManual(string tag, string requestSummary, string responseSummary, bool isError)
    {
        DebugFileLogger.Log($"[HTTP {tag}] {requestSummary} -> {responseSummary}");
        OnHttpEvent?.Invoke(tag, requestSummary, responseSummary, isError);
    }

    // ── Internal helpers ───────────────────────────────────

    internal static string FormatHeaders(System.Net.Http.Headers.HttpHeaders primary,
                                         System.Net.Http.Headers.HttpHeaders? content = null)
    {
        var sb = new StringBuilder();
        AppendHeaders(sb, primary);
        if (content != null) AppendHeaders(sb, content);
        return sb.ToString().TrimEnd();
    }

    private static void AppendHeaders(StringBuilder sb, System.Net.Http.Headers.HttpHeaders headers)
    {
        foreach (var (k, vs) in headers)
        {
            var value = SensitiveHeaders.Contains(k) ? Redact(string.Join(",", vs)) : string.Join(",", vs);
            sb.Append(k).Append(": ").Append(value).Append('\n');
        }
    }

    internal static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 8) return "***";
        return value[..4] + "..." + value[^4..] + $" (len={value.Length})";
    }

    internal static string Truncate(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Length <= MaxBodyChars) return s;
        return s[..MaxBodyChars] + $"\n...[truncated, total {s.Length} chars]";
    }

    /// <summary>
    /// Mask base64 audio payloads in JSON bodies so logs don't blow up.
    /// </summary>
    internal static string ScrubBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        // Mask any "data": "<base64>" longer than 64 chars
        return Regex.Replace(body,
            "\"data\"\\s*:\\s*\"([A-Za-z0-9+/=]{64,})\"",
            m => "\"data\":\"<base64 " + m.Groups[1].Value.Length + " chars>\"");
    }
}

internal sealed class LoggingHandler : DelegatingHandler
{
    private readonly string _tag;

    public LoggingHandler(string tag) { _tag = tag; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var reqHeaders = HttpLogger.FormatHeaders(request.Headers,
            request.Content?.Headers);
        var reqBody = "";
        if (request.Content != null)
        {
            try
            {
                reqBody = await request.Content.ReadAsStringAsync(ct);
                reqBody = HttpLogger.ScrubBody(reqBody);
            }
            catch (Exception ex) { reqBody = $"<failed to read: {ex.Message}>"; }
        }

        var requestSummary =
            $"{request.Method} {request.RequestUri}\n{reqHeaders}\n\n{HttpLogger.Truncate(reqBody)}";

        HttpResponseMessage? response = null;
        try
        {
            response = await base.SendAsync(request, ct);
            sw.Stop();

            var respHeaders = HttpLogger.FormatHeaders(response.Headers,
                response.Content.Headers);
            var respBody = "";
            try { respBody = await response.Content.ReadAsStringAsync(ct); }
            catch (Exception ex) { respBody = $"<failed to read: {ex.Message}>"; }

            // Re-create content so the caller can still read it.
            var bytes = Encoding.UTF8.GetBytes(respBody);
            var newContent = new ByteArrayContent(bytes);
            foreach (var (k, v) in response.Content.Headers)
                newContent.Headers.TryAddWithoutValidation(k, v);
            response.Content = newContent;

            var responseSummary =
                $"{(int)response.StatusCode} {response.StatusCode} ({sw.ElapsedMilliseconds}ms)\n" +
                $"{respHeaders}\n\n{HttpLogger.Truncate(respBody)}";

            var isError = !response.IsSuccessStatusCode;
            HttpLogger.LogManual(_tag, requestSummary, responseSummary, isError);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            HttpLogger.LogManual(_tag, requestSummary,
                $"<exception after {sw.ElapsedMilliseconds}ms> {ex.GetType().Name}: {ex.Message}",
                isError: true);
            throw;
        }
    }
}
