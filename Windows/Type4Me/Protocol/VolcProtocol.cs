using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Type4Me.Protocol;

/// <summary>
/// Parsed server response.
/// </summary>
public sealed class VolcUtterance
{
    public string Text { get; init; } = string.Empty;
    public bool Definite { get; init; }
}

public sealed class VolcASRResult
{
    public string Text { get; init; } = string.Empty;
    public VolcUtterance[] Utterances { get; init; } = [];
}

public sealed class VolcServerResponse
{
    public VolcHeader Header { get; init; }
    public VolcASRResult Result { get; init; } = new();
}

/// <summary>
/// Volcengine binary protocol encoder/decoder.
/// </summary>
public static class VolcProtocol
{
    // ── Build Client Request JSON ──────────────────────────

    public static byte[] BuildClientRequest(
        string uid,
        string format = "pcm",
        string codec = "raw",
        int rate = 16000,
        int bits = 16,
        int channel = 1,
        bool showUtterances = true,
        string resultType = "full",
        ASR.ASRRequestOptions? options = null)
    {
        options ??= new ASR.ASRRequestOptions();

        var request = new Dictionary<string, object>
        {
            ["model_name"] = "bigmodel",
            ["enable_punc"] = options.EnablePunc,
            ["enable_ddc"] = true,
            ["enable_nonstream"] = true,
            ["show_utterances"] = showUtterances,
            ["result_type"] = resultType,
            ["end_window_size"] = 1500,
            ["force_to_speech_time"] = 1000,
        };

        var contextString = BuildContextString(options.Hotwords);
        if (contextString != null)
            request["context"] = contextString;

        var corpus = new Dictionary<string, object>();
        var trimmedBoosting = options.BoostingTableID?.Trim();
        if (!string.IsNullOrEmpty(trimmedBoosting))
            corpus["boosting_table_id"] = trimmedBoosting;
        if (corpus.Count > 0)
            request["corpus"] = corpus;

        if (options.ContextHistoryLength > 0)
            request["context_history_length"] = options.ContextHistoryLength;

        var payload = new Dictionary<string, object>
        {
            ["user"] = new Dictionary<string, object> { ["uid"] = uid },
            ["audio"] = new Dictionary<string, object>
            {
                ["format"] = format,
                ["codec"] = codec,
                ["rate"] = rate,
                ["bits"] = bits,
                ["channel"] = channel,
            },
            ["request"] = request,
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private static string? BuildContextString(string[] hotwords)
    {
        var cleaned = hotwords
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .ToArray();

        if (cleaned.Length == 0) return null;

        var contextObject = new Dictionary<string, object>
        {
            ["hotwords"] = cleaned.Select(w => new Dictionary<string, string> { ["word"] = w }).ToArray(),
        };

        return JsonSerializer.Serialize(contextObject);
    }

    // ── Encode Full Binary Message ─────────────────────────

    public static byte[] EncodeMessage(VolcHeader header, byte[] payload, int? sequenceNumber = null)
    {
        using var ms = new MemoryStream();

        // Header (4 bytes)
        ms.Write(header.Encode());

        // Optional sequence number (4 bytes big-endian)
        if (sequenceNumber.HasValue)
        {
            Span<byte> seqBytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(seqBytes, sequenceNumber.Value);
            ms.Write(seqBytes);
        }

        // Payload size (4 bytes big-endian) + payload
        Span<byte> sizeBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)payload.Length);
        ms.Write(sizeBytes);
        ms.Write(payload);

        return ms.ToArray();
    }

    // ── Encode Audio Packet ────────────────────────────────

    public static byte[] EncodeAudioPacket(byte[] audioData, bool isLast)
    {
        var flags = isLast ? VolcMessageFlags.LastPacketNoSequence : VolcMessageFlags.NoSequence;
        var header = new VolcHeader(VolcMessageType.AudioOnlyRequest, flags);
        return EncodeMessage(header, audioData);
    }

    // ── Decode Server Response ─────────────────────────────

    public static VolcServerResponse DecodeServerResponse(ReadOnlySpan<byte> data)
    {
        var header = VolcHeader.Decode(data);
        int headerBytes = header.HeaderSize * 4;
        int offset = headerBytes;

        // Skip sequence number if present
        if (header.HasSequence)
            offset += 4;

        if (data.Length < offset + 4)
            throw new VolcProtocolException("Invalid payload: too short for size field");

        // Read payload size (4 bytes big-endian)
        uint payloadSize = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
        offset += 4;

        if (data.Length < offset + (int)payloadSize)
            throw new VolcProtocolException("Invalid payload: insufficient data");

        var payload = data.Slice(offset, (int)payloadSize).ToArray();

        // Handle server error
        if (header.MessageType == VolcMessageType.ServerError)
        {
            if (header.Compression == VolcCompression.Gzip)
                payload = GzipDecompress(payload);

            if (header.Serialization == VolcSerialization.Json && payload.Length > 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    var root = doc.RootElement;
                    int? code = root.TryGetProperty("code", out var c) ? c.GetInt32() : null;
                    string? message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                    throw new VolcProtocolException($"Server error: {message}", code);
                }
                catch (VolcProtocolException) { throw; }
                catch { /* fall through */ }
            }
            throw new VolcProtocolException("Server error (no details)");
        }

        // Decompress if needed
        if (header.Compression == VolcCompression.Gzip)
            payload = GzipDecompress(payload);

        // Parse JSON
        if (header.Serialization != VolcSerialization.Json)
            throw new VolcProtocolException("Expected JSON serialization");

        using var jsonDoc = JsonDocument.Parse(payload);
        var json = jsonDoc.RootElement;

        // Response format: {"result": {"text": "...", "utterances": [...]}}
        string text = "";
        var utterances = new List<VolcUtterance>();

        JsonElement resultObj = default;
        bool hasResult = json.TryGetProperty("result", out resultObj);

        if (hasResult && resultObj.TryGetProperty("text", out var textEl))
            text = textEl.GetString() ?? "";
        else if (json.TryGetProperty("text", out var topText))
            text = topText.GetString() ?? "";

        JsonElement uttsEl = default;
        bool hasUtts = hasResult && resultObj.TryGetProperty("utterances", out uttsEl);
        if (!hasUtts)
            json.TryGetProperty("utterances", out uttsEl);

        if (uttsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var u in uttsEl.EnumerateArray())
            {
                utterances.Add(new VolcUtterance
                {
                    Text = u.TryGetProperty("text", out var ut) ? ut.GetString() ?? "" : "",
                    Definite = u.TryGetProperty("definite", out var ud) && ud.GetBoolean(),
                });
            }
        }

        return new VolcServerResponse
        {
            Header = header,
            Result = new VolcASRResult
            {
                Text = text,
                Utterances = utterances.ToArray(),
            },
        };
    }

    // ── GZip ───────────────────────────────────────────────

    public static byte[] GzipCompress(byte[] data)
    {
        if (data.Length == 0) return [];
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }

    public static byte[] GzipDecompress(byte[] data)
    {
        if (data.Length == 0) return [];
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
