using System.Net.Http;
using System.Text.Json;
using Type4Me.ASR.Providers;
using Type4Me.Audio;
using Type4Me.Services;

namespace Type4Me.ASR;

/// <summary>
/// One-shot HTTP ASR using Volcengine's flash recognition API.
/// Sends complete recorded audio as a WAV file and returns the full text.
/// </summary>
public static class VolcFlashASRClient
{
    private static readonly Uri Endpoint =
        new("https://openspeech.bytedance.com/api/v3/auc/bigmodel/recognize/flash");

    private static readonly HttpClient _httpClient = new();

    public static async Task<string> RecognizeAsync(byte[] pcmData, VolcanoASRConfig config, CancellationToken ct = default)
    {
        if (pcmData.Length == 0)
            throw new InvalidOperationException("No audio data to recognize");

        var wavData = WavEncoder.Encode(pcmData);
        var base64Audio = Convert.ToBase64String(wavData);

        var body = new
        {
            user = new { uid = config.Uid },
            audio = new { data = base64Audio },
            request = new
            {
                model_name = "bigmodel",
                enable_punc = true,
            },
        };

        var resourceId = Environment.GetEnvironmentVariable("VOLC_FLASH_RESOURCE_ID")
            ?? "volc.bigasr.auc_turbo";

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json");
        request.Headers.Add("X-Api-App-Key", config.AppKey);
        request.Headers.Add("X-Api-Access-Key", config.AccessKey);
        request.Headers.Add("X-Api-Resource-Id", resourceId);
        request.Headers.Add("X-Api-Request-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-Api-Sequence", "-1");

        DebugFileLogger.Log(
            $"[FlashASR] Sending {wavData.Length} bytes WAV ({base64Audio.Length} bytes base64), resourceId={resourceId}");

        using var response = await _httpClient.SendAsync(request, ct);

        var statusCode = response.Headers.TryGetValues("X-Api-Status-Code", out var codes)
            ? codes.FirstOrDefault() ?? response.StatusCode.ToString()
            : response.StatusCode.ToString();

        var logId = response.Headers.TryGetValues("X-Tt-Logid", out var logIds)
            ? logIds.FirstOrDefault() ?? "?"
            : "?";

        DebugFileLogger.Log($"[FlashASR] Response status={statusCode}, logId={logId}");

        if (statusCode != "20000000")
        {
            var message = response.Headers.TryGetValues("X-Api-Message", out var msgs)
                ? msgs.FirstOrDefault() ?? "Unknown"
                : "Unknown";
            throw new InvalidOperationException($"Flash ASR error {statusCode}: {message}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("result", out var resultEl) &&
            resultEl.TryGetProperty("text", out var textEl))
        {
            var text = textEl.GetString() ?? "";
            DebugFileLogger.Log($"[FlashASR] Recognized: {text}");
            return text;
        }

        DebugFileLogger.Log($"[FlashASR] Unexpected response: {responseBody[..Math.Min(500, responseBody.Length)]}");
        throw new InvalidOperationException("Invalid response from flash ASR");
    }
}
