using System.Net.WebSockets;
using System.Threading.Channels;
using Type4Me.ASR.Providers;
using Type4Me.Models;
using Type4Me.Protocol;
using Type4Me.Services;

namespace Type4Me.ASR;

/// <summary>
/// Streaming WebSocket ASR client for Volcengine's bigmodel_async endpoint.
/// </summary>
public sealed class VolcASRClient : ISpeechRecognizer
{
    private static readonly Uri Endpoint =
        new("wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async");

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    private readonly Channel<RecognitionEvent> _eventChannel =
        Channel.CreateUnbounded<RecognitionEvent>(new UnboundedChannelOptions { SingleReader = true });

    private int _audioPacketCount;
    private int _totalAudioBytes;
    private RecognitionTranscript _lastTranscript = new();

    public ChannelReader<RecognitionEvent> Events => _eventChannel.Reader;

    // ── Connect ────────────────────────────────────────────

    public async Task ConnectAsync(Dictionary<string, string> credentials, ASRRequestOptions options, CancellationToken ct = default)
    {
        var config = VolcanoASRConfig.TryCreate(credentials)
            ?? throw new InvalidOperationException("VolcASRClient requires valid VolcanoASRConfig");

        var connectId = Guid.NewGuid().ToString();

        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("X-Api-App-Key", config.AppKey);
        _webSocket.Options.SetRequestHeader("X-Api-Access-Key", config.AccessKey);
        _webSocket.Options.SetRequestHeader("X-Api-Resource-Id", config.ResourceId);
        _webSocket.Options.SetRequestHeader("X-Api-Connect-Id", connectId);

        await _webSocket.ConnectAsync(Endpoint, ct);

        // Send full_client_request
        var payload = VolcProtocol.BuildClientRequest(uid: config.Uid, options: options);
        var header = new VolcHeader(
            VolcMessageType.FullClientRequest,
            VolcMessageFlags.NoSequence,
            VolcSerialization.Json,
            VolcCompression.None);
        var message = VolcProtocol.EncodeMessage(header, payload);

        _lastTranscript = new RecognitionTranscript();
        _audioPacketCount = 0;
        _totalAudioBytes = 0;

        DebugFileLogger.Log($"[ASR] Sending full_client_request ({message.Length} bytes), connectId={connectId}");

        await _webSocket.SendAsync(message, WebSocketMessageType.Binary, true, ct);
        DebugFileLogger.Log("[ASR] full_client_request sent OK");

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
    }

    // ── Send Audio ─────────────────────────────────────────

    public async Task SendAudioAsync(byte[] data, CancellationToken ct = default)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        _audioPacketCount++;
        _totalAudioBytes += data.Length;

        var packet = VolcProtocol.EncodeAudioPacket(data, isLast: false);
        await _webSocket.SendAsync(packet, WebSocketMessageType.Binary, true, ct);
    }

    // ── End Audio ──────────────────────────────────────────

    public async Task EndAudioAsync(CancellationToken ct = default)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        var packet = VolcProtocol.EncodeAudioPacket([], isLast: true);
        await _webSocket.SendAsync(packet, WebSocketMessageType.Binary, true, ct);
        DebugFileLogger.Log("[ASR] Sent last audio packet (empty, isLast=true)");
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
        DebugFileLogger.Log("[ASR] Disconnected");
    }

    // ── Receive Loop ───────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];

        try
        {
            while (!ct.IsCancellationRequested && _webSocket is { State: WebSocketState.Open })
            {
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary)
                    HandleMessage(ms.ToArray());
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[ASR] Receive loop error: {ex.Message}");
            if (!ct.IsCancellationRequested)
            {
                if (_audioPacketCount == 0)
                    EmitEvent(new RecognitionEvent.Error(ex));
                else
                    DebugFileLogger.Log($"[ASR] Treating as normal session end (sent {_audioPacketCount} packets)");

                EmitEvent(new RecognitionEvent.Completed());
            }
        }

        DebugFileLogger.Log("[ASR] Receive loop ended");
        _eventChannel.Writer.TryComplete();
    }

    private void HandleMessage(byte[] data)
    {
        if (data.Length < 2) return;

        byte msgType = (byte)((data[1] >> 4) & 0x0F);

        // Server error (0xF): could be real error or session-complete signal
        if (msgType == 0x0F)
        {
            if (_audioPacketCount == 0)
            {
                try
                {
                    VolcProtocol.DecodeServerResponse(data);
                }
                catch (Exception ex)
                {
                    DebugFileLogger.Log($"[ASR] Server error: {ex.Message}");
                    EmitEvent(new RecognitionEvent.Error(ex));
                }
            }
            else
            {
                DebugFileLogger.Log($"[ASR] Session ended by server after {_audioPacketCount} audio packets");
            }

            EmitEvent(new RecognitionEvent.Completed());

            // Close WebSocket
            try
            {
                _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch { /* ignore */ }
            return;
        }

        try
        {
            var response = VolcProtocol.DecodeServerResponse(data);
            var transcript = MakeTranscript(response.Result, response.Header.Flags == VolcMessageFlags.AsyncFinal);

            // Deduplicate identical transcripts
            if (transcript.ComposedText == _lastTranscript.ComposedText &&
                transcript.IsFinal == _lastTranscript.IsFinal)
                return;

            _lastTranscript = transcript;

            DebugFileLogger.Log(
                $"[ASR] Transcript update confirmed={transcript.ConfirmedSegments.Length} " +
                $"partial={transcript.PartialText.Length} final={transcript.IsFinal}");

            EmitEvent(new RecognitionEvent.Transcript(transcript));

            if (transcript.IsFinal && !string.IsNullOrEmpty(transcript.AuthoritativeText))
                DebugFileLogger.Log($"[ASR] Final transcript: '{transcript.AuthoritativeText}'");
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[ASR] Decode error: {ex.Message}");
            EmitEvent(new RecognitionEvent.Error(ex));
        }
    }

    private void EmitEvent(RecognitionEvent evt)
    {
        _eventChannel.Writer.TryWrite(evt);
    }

    private static RecognitionTranscript MakeTranscript(VolcASRResult result, bool isFinal)
    {
        var confirmedSegments = result.Utterances
            .Where(u => u.Definite && !string.IsNullOrEmpty(u.Text))
            .Select(u => u.Text)
            .ToArray();

        var partialText = result.Utterances
            .LastOrDefault(u => !u.Definite && !string.IsNullOrEmpty(u.Text))?.Text ?? "";

        var composedParts = confirmedSegments.ToList();
        if (!string.IsNullOrEmpty(partialText))
            composedParts.Add(partialText);
        var composedText = string.Join("", composedParts);

        var authoritativeText = string.IsNullOrEmpty(result.Text) ? composedText : result.Text;

        return new RecognitionTranscript
        {
            ConfirmedSegments = confirmedSegments,
            PartialText = partialText,
            AuthoritativeText = authoritativeText,
            IsFinal = isFinal,
        };
    }

    public void Dispose()
    {
        _receiveCts?.Cancel();
        _webSocket?.Dispose();
        _eventChannel.Writer.TryComplete();
    }
}
