using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Type4Me.ASR.Providers;
using Type4Me.Audio;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.ASR;

/// <summary>
/// Azure Speech Service real-time streaming ASR client.
/// Uses the Speech-to-Text WebSocket v1 protocol.
/// Audio format: 16kHz / 16-bit / mono PCM (RIFF header sent once).
/// </summary>
public sealed class AzureASRClient : ISpeechRecognizer
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    private readonly Channel<RecognitionEvent> _eventChannel =
        Channel.CreateUnbounded<RecognitionEvent>(new UnboundedChannelOptions { SingleReader = true });

    private RecognitionTranscript _lastTranscript = new();
    private string _language = "zh-CN";

    public ChannelReader<RecognitionEvent> Events => _eventChannel.Reader;

    // ── Connect ────────────────────────────────────────────

    public async Task ConnectAsync(Dictionary<string, string> credentials, ASRRequestOptions options, CancellationToken ct = default)
    {
        var config = AzureASRConfig.TryCreate(credentials)
            ?? throw new InvalidOperationException("AzureASRClient requires valid AzureASRConfig");

        // Determine language (from credentials or default)
        _language = credentials.GetValueOrDefault("language", "zh-CN");

        // Build WebSocket URL
        var connectionId = Guid.NewGuid().ToString("N");
        var endpoint = !string.IsNullOrEmpty(config.CustomEndpoint)
            ? config.CustomEndpoint.TrimEnd('/')
            : $"wss://{config.Region}.stt.speech.microsoft.com";

        var url = $"{endpoint}/speech/recognition/conversation/cognitiveservices/v1" +
                  $"?language={_language}&format=detailed";

        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Ocp-Apim-Subscription-Key", config.SubscriptionKey);
        _webSocket.Options.SetRequestHeader("X-ConnectionId", connectionId);

        DebugFileLogger.Log($"[AzureASR] Connecting to {config.Region}, language={_language}, connectionId={connectionId}");

        await _webSocket.ConnectAsync(new Uri(url), ct);

        // Send speech.config message
        var speechConfig = BuildSpeechConfig();
        await SendTextMessage("speech.config", speechConfig, ct);

        // Send RIFF/WAV header for audio format (16kHz/16-bit/mono PCM)
        var riffHeader = BuildRiffHeader();
        await SendBinaryWithHeader("audio", riffHeader, ct);

        _lastTranscript = new RecognitionTranscript();

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

        DebugFileLogger.Log("[AzureASR] Connected and configured");
    }

    // ── Send Audio ─────────────────────────────────────────

    public async Task SendAudioAsync(byte[] data, CancellationToken ct = default)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        await SendBinaryWithHeader("audio", data, ct);
    }

    // ── End Audio ──────────────────────────────────────────

    public async Task EndAudioAsync(CancellationToken ct = default)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        // Send empty audio message to signal end of stream
        await SendBinaryWithHeader("audio", [], ct);
        DebugFileLogger.Log("[AzureASR] Sent end-of-audio signal");
    }

    // ── Disconnect ─────────────────────────────────────────

    public async Task DisconnectAsync()
    {
        _receiveCts?.Cancel();

        if (_webSocket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { /* ignore close errors */ }
        }

        _webSocket?.Dispose();
        _webSocket = null;

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { /* ignore */ }
            _receiveTask = null;
        }

        _eventChannel.Writer.TryComplete();
        DebugFileLogger.Log("[AzureASR] Disconnected");
    }

    // ── Receive Loop ───────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];

        try
        {
            while (!ct.IsCancellationRequested && _webSocket is { State: WebSocketState.Open })
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    HandleTextMessage(text);
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[AzureASR] Receive loop error: {ex.Message}");
            if (!ct.IsCancellationRequested)
            {
                EmitEvent(new RecognitionEvent.Error(ex));
                EmitEvent(new RecognitionEvent.Completed());
            }
        }

        DebugFileLogger.Log("[AzureASR] Receive loop ended");
        _eventChannel.Writer.TryComplete();
    }

    private void HandleTextMessage(string message)
    {
        // Azure Speech sends text messages with a header section followed by body
        // Format: "Path: speech.hypothesis\r\n...\r\n\r\n{json}"
        var headerEnd = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerEnd < 0) return;

        var headerSection = message[..headerEnd];
        var body = message[(headerEnd + 4)..];

        var path = "";
        foreach (var line in headerSection.Split("\r\n"))
        {
            if (line.StartsWith("Path:", StringComparison.OrdinalIgnoreCase))
            {
                path = line[5..].Trim();
                break;
            }
        }

        switch (path)
        {
            case "speech.hypothesis":
                HandleHypothesis(body);
                break;

            case "speech.phrase":
                HandlePhrase(body);
                break;

            case "speech.endDetected":
                DebugFileLogger.Log("[AzureASR] End of speech detected");
                break;

            case "turn.end":
                DebugFileLogger.Log("[AzureASR] Turn ended");
                EmitEvent(new RecognitionEvent.Completed());
                break;

            case "turn.start":
                DebugFileLogger.Log("[AzureASR] Turn started");
                break;

            case "speech.startDetected":
                DebugFileLogger.Log("[AzureASR] Speech start detected");
                break;

            default:
                DebugFileLogger.Log($"[AzureASR] Received: {path}");
                break;
        }
    }

    private void HandleHypothesis(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var text = root.GetProperty("Text").GetString() ?? "";

            var transcript = new RecognitionTranscript
            {
                ConfirmedSegments = [],
                PartialText = text,
                IsFinal = false,
            };

            if (transcript.ComposedText == _lastTranscript.ComposedText) return;
            _lastTranscript = transcript;

            DebugFileLogger.Log($"[AzureASR] Hypothesis: {text}");
            EmitEvent(new RecognitionEvent.Transcript(transcript));
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[AzureASR] Hypothesis parse error: {ex.Message}");
        }
    }

    private void HandlePhrase(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = root.GetProperty("RecognitionStatus").GetString();
            if (status != "Success")
            {
                DebugFileLogger.Log($"[AzureASR] Phrase status: {status}");
                if (status == "EndOfDictation" || status == "InitialSilenceTimeout")
                {
                    // Not really an error, just no speech
                    return;
                }
                return;
            }

            // Try to get best result from NBest array (detailed format)
            var displayText = "";
            if (root.TryGetProperty("NBest", out var nbest) && nbest.GetArrayLength() > 0)
            {
                displayText = nbest[0].TryGetProperty("Display", out var disp)
                    ? disp.GetString() ?? ""
                    : root.TryGetProperty("DisplayText", out var dt) ? dt.GetString() ?? "" : "";
            }
            else if (root.TryGetProperty("DisplayText", out var dt2))
            {
                displayText = dt2.GetString() ?? "";
            }

            if (string.IsNullOrEmpty(displayText)) return;

            var transcript = new RecognitionTranscript
            {
                ConfirmedSegments = [displayText],
                PartialText = "",
                AuthoritativeText = displayText,
                IsFinal = true,
            };

            _lastTranscript = transcript;

            DebugFileLogger.Log($"[AzureASR] Final phrase: {displayText}");
            EmitEvent(new RecognitionEvent.Transcript(transcript));
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[AzureASR] Phrase parse error: {ex.Message}");
        }
    }

    // ── Protocol Helpers ───────────────────────────────────

    /// <summary>
    /// Send a text message with Azure Speech protocol headers.
    /// </summary>
    private async Task SendTextMessage(string path, string body, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var message = $"Path: {path}\r\nX-RequestId: {requestId}\r\nX-Timestamp: {timestamp}\r\nContent-Type: application/json\r\n\r\n{body}";

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Send a binary message with a 2-byte header length prefix + path header + body.
    /// Azure Speech binary messages: [2-byte header length][header string][binary payload]
    /// </summary>
    private async Task SendBinaryWithHeader(string path, byte[] body, CancellationToken ct)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        var requestId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var header = $"Path: {path}\r\nX-RequestId: {requestId}\r\nX-Timestamp: {timestamp}\r\nContent-Type: audio/x-wav";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        var headerLen = (ushort)headerBytes.Length;

        // Message format: [2-byte big-endian header length][header][audio data]
        var message = new byte[2 + headerBytes.Length + body.Length];
        message[0] = (byte)(headerLen >> 8);
        message[1] = (byte)(headerLen & 0xFF);
        Buffer.BlockCopy(headerBytes, 0, message, 2, headerBytes.Length);
        Buffer.BlockCopy(body, 0, message, 2 + headerBytes.Length, body.Length);

        await _webSocket.SendAsync(message, WebSocketMessageType.Binary, true, ct);
    }

    /// <summary>
    /// Build the speech.config JSON payload.
    /// </summary>
    private static string BuildSpeechConfig()
    {
        var config = new
        {
            context = new
            {
                system = new { version = "1.0.00000" },
                os = new { platform = "Windows", name = "Type4Me", version = "1.0" },
                device = new { manufacturer = "Type4Me", model = "Desktop", version = "1.0" },
            }
        };
        return JsonSerializer.Serialize(config);
    }

    /// <summary>
    /// Build a RIFF/WAV header for 16kHz/16-bit/mono PCM.
    /// This tells Azure Speech the audio format.
    /// </summary>
    private static byte[] BuildRiffHeader()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int sampleRate = AudioCaptureEngine.SampleRate;
        short channels = AudioCaptureEngine.Channels;
        short bitsPerSample = AudioCaptureEngine.BitsPerSample;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(0); // file size placeholder (streaming)
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);           // chunk size
        bw.Write((short)1);     // PCM format
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);

        // data chunk
        bw.Write("data"u8);
        bw.Write(0); // data size placeholder (streaming)

        bw.Flush();
        return ms.ToArray();
    }

    private void EmitEvent(RecognitionEvent evt)
    {
        _eventChannel.Writer.TryWrite(evt);
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _webSocket?.Dispose();
        _eventChannel.Writer.TryComplete();
    }
}
