using System.IO;
using System.Text;
using System.Text.Json;
using Type4Me.Protocol;
using Xunit;

namespace Type4Me.Tests;

/// <summary>
/// Tests for VolcHeader encode/decode and VolcProtocol message framing.
/// </summary>
public class VolcProtocolTests
{
    [Fact]
    public void Header_EncodeAndDecode_RoundTrips()
    {
        var original = new VolcHeader(
            VolcMessageType.FullClientRequest,
            VolcMessageFlags.PositiveSequence,
            VolcSerialization.Json,
            VolcCompression.Gzip);

        var encoded = original.Encode();
        Assert.Equal(4, encoded.Length);

        var decoded = VolcHeader.Decode(encoded);

        Assert.Equal(original.Version, decoded.Version);
        Assert.Equal(original.HeaderSize, decoded.HeaderSize);
        Assert.Equal(original.MessageType, decoded.MessageType);
        Assert.Equal(original.Flags, decoded.Flags);
        Assert.Equal(original.Serialization, decoded.Serialization);
        Assert.Equal(original.Compression, decoded.Compression);
    }

    [Fact]
    public void Header_AudioRequest_EncodeDecodeRoundTrips()
    {
        var header = new VolcHeader(
            VolcMessageType.AudioOnlyRequest,
            VolcMessageFlags.NoSequence);

        var encoded = header.Encode();
        var decoded = VolcHeader.Decode(encoded);

        Assert.Equal(VolcMessageType.AudioOnlyRequest, decoded.MessageType);
        Assert.Equal(VolcMessageFlags.NoSequence, decoded.Flags);
        Assert.Equal(VolcSerialization.None, decoded.Serialization);
        Assert.Equal(VolcCompression.None, decoded.Compression);
    }

    [Fact]
    public void Header_HasSequence_CorrectForFlags()
    {
        Assert.True(new VolcHeader { Flags = VolcMessageFlags.PositiveSequence }.HasSequence);
        Assert.True(new VolcHeader { Flags = VolcMessageFlags.NegativeSequenceLast }.HasSequence);
        Assert.False(new VolcHeader { Flags = VolcMessageFlags.NoSequence }.HasSequence);
        Assert.False(new VolcHeader { Flags = VolcMessageFlags.LastPacketNoSequence }.HasSequence);
    }

    [Fact]
    public void Header_Decode_ThrowsOnShortData()
    {
        Assert.Throws<VolcProtocolException>(() => VolcHeader.Decode(new byte[] { 0x11 }));
        Assert.Throws<VolcProtocolException>(() => VolcHeader.Decode(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void GzipCompressDecompress_RoundTrips()
    {
        var original = Encoding.UTF8.GetBytes("Hello, Volcengine! This is a test message for compression.");
        var compressed = VolcProtocol.GzipCompress(original);

        Assert.NotEmpty(compressed);
        Assert.NotEqual(original, compressed);

        var decompressed = VolcProtocol.GzipDecompress(compressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void GzipCompress_EmptyData_ReturnsEmpty()
    {
        var result = VolcProtocol.GzipCompress([]);
        Assert.Empty(result);
    }

    [Fact]
    public void GzipDecompress_EmptyData_ReturnsEmpty()
    {
        var result = VolcProtocol.GzipDecompress([]);
        Assert.Empty(result);
    }

    [Fact]
    public void EncodeAudioPacket_NotLast_ProducesCorrectFormat()
    {
        var audio = new byte[] { 1, 2, 3, 4 };
        var packet = VolcProtocol.EncodeAudioPacket(audio, isLast: false);

        // Decode header from the packet
        var header = VolcHeader.Decode(packet);
        Assert.Equal(VolcMessageType.AudioOnlyRequest, header.MessageType);
        Assert.Equal(VolcMessageFlags.NoSequence, header.Flags);

        // 4 header + 4 size + 4 audio = 12 bytes
        Assert.Equal(12, packet.Length);
    }

    [Fact]
    public void EncodeAudioPacket_Last_SetsLastFlag()
    {
        var audio = new byte[] { 10, 20 };
        var packet = VolcProtocol.EncodeAudioPacket(audio, isLast: true);

        var header = VolcHeader.Decode(packet);
        Assert.Equal(VolcMessageFlags.LastPacketNoSequence, header.Flags);
    }

    [Fact]
    public void EncodeMessage_WithSequenceNumber_IncludesSequence()
    {
        var header = new VolcHeader(
            VolcMessageType.FullClientRequest,
            VolcMessageFlags.PositiveSequence,
            VolcSerialization.Json,
            VolcCompression.None);

        var payload = Encoding.UTF8.GetBytes("{}");
        var message = VolcProtocol.EncodeMessage(header, payload, sequenceNumber: 42);

        // 4 header + 4 seq + 4 size + 2 payload = 14
        Assert.Equal(14, message.Length);
    }

    [Fact]
    public void BuildClientRequest_ProducesValidJson()
    {
        var requestBytes = VolcProtocol.BuildClientRequest("test-uid");
        var json = JsonDocument.Parse(requestBytes);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("user", out var user));
        Assert.Equal("test-uid", user.GetProperty("uid").GetString());

        Assert.True(root.TryGetProperty("audio", out var audio));
        Assert.Equal("pcm", audio.GetProperty("format").GetString());
        Assert.Equal(16000, audio.GetProperty("rate").GetInt32());

        Assert.True(root.TryGetProperty("request", out var request));
        Assert.Equal("bigmodel", request.GetProperty("model_name").GetString());
    }

    [Fact]
    public void BuildClientRequest_WithHotwords_IncludesContext()
    {
        var options = new ASR.ASRRequestOptions
        {
            Hotwords = ["claude", "anthropic"],
        };
        var requestBytes = VolcProtocol.BuildClientRequest("uid-1", options: options);
        var json = JsonDocument.Parse(requestBytes);
        var request = json.RootElement.GetProperty("request");

        Assert.True(request.TryGetProperty("context", out var ctx));
        var contextStr = ctx.GetString();
        Assert.NotNull(contextStr);
        Assert.Contains("claude", contextStr);
        Assert.Contains("anthropic", contextStr);
    }

    [Fact]
    public void BuildClientRequest_WithBoostingTable_IncludesCorpus()
    {
        var options = new ASR.ASRRequestOptions
        {
            BoostingTableID = "bt-12345",
        };
        var requestBytes = VolcProtocol.BuildClientRequest("uid-1", options: options);
        var json = JsonDocument.Parse(requestBytes);
        var request = json.RootElement.GetProperty("request");

        Assert.True(request.TryGetProperty("corpus", out var corpus));
        Assert.Equal("bt-12345", corpus.GetProperty("boosting_table_id").GetString());
    }

    [Fact]
    public void DecodeServerResponse_ParsesResultCorrectly()
    {
        // Build a server response manually
        var resultJson = """{"result":{"text":"你好世界","utterances":[{"text":"你好世界","definite":true}]}}""";
        var payloadBytes = Encoding.UTF8.GetBytes(resultJson);

        var header = new VolcHeader
        {
            Version = 1,
            HeaderSize = 1,
            MessageType = VolcMessageType.ServerResponse,
            Flags = VolcMessageFlags.NoSequence,
            Serialization = VolcSerialization.Json,
            Compression = VolcCompression.None,
        };

        var headerBytes = header.Encode();

        // Build full message: header(4) + payloadSize(4) + payload
        using var ms = new MemoryStream();
        ms.Write(headerBytes);
        var sizeBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)payloadBytes.Length);
        ms.Write(sizeBytes);
        ms.Write(payloadBytes);

        var response = VolcProtocol.DecodeServerResponse(ms.ToArray());

        Assert.Equal("你好世界", response.Result.Text);
        Assert.Single(response.Result.Utterances);
        Assert.Equal("你好世界", response.Result.Utterances[0].Text);
        Assert.True(response.Result.Utterances[0].Definite);
    }

    [Fact]
    public void DecodeServerResponse_WithGzip_Decompresses()
    {
        var resultJson = """{"result":{"text":"compressed","utterances":[]}}""";
        var plainPayload = Encoding.UTF8.GetBytes(resultJson);
        var compressedPayload = VolcProtocol.GzipCompress(plainPayload);

        var header = new VolcHeader
        {
            Version = 1,
            HeaderSize = 1,
            MessageType = VolcMessageType.ServerResponse,
            Flags = VolcMessageFlags.NoSequence,
            Serialization = VolcSerialization.Json,
            Compression = VolcCompression.Gzip,
        };

        using var ms = new MemoryStream();
        ms.Write(header.Encode());
        var sizeBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)compressedPayload.Length);
        ms.Write(sizeBytes);
        ms.Write(compressedPayload);

        var response = VolcProtocol.DecodeServerResponse(ms.ToArray());
        Assert.Equal("compressed", response.Result.Text);
    }

    [Fact]
    public void DecodeServerResponse_ServerError_ThrowsProtocolException()
    {
        var errorJson = """{"code":1001,"message":"Authentication failed"}""";
        var payloadBytes = Encoding.UTF8.GetBytes(errorJson);

        var header = new VolcHeader
        {
            Version = 1,
            HeaderSize = 1,
            MessageType = VolcMessageType.ServerError,
            Flags = VolcMessageFlags.NoSequence,
            Serialization = VolcSerialization.Json,
            Compression = VolcCompression.None,
        };

        using var ms = new MemoryStream();
        ms.Write(header.Encode());
        var sizeBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)payloadBytes.Length);
        ms.Write(sizeBytes);
        ms.Write(payloadBytes);

        var ex = Assert.Throws<VolcProtocolException>(() =>
            VolcProtocol.DecodeServerResponse(ms.ToArray()));

        Assert.Contains("Authentication failed", ex.Message);
        Assert.Equal(1001, ex.ErrorCode);
    }

    [Fact]
    public void Header_Equality_Works()
    {
        var h1 = new VolcHeader(VolcMessageType.FullClientRequest, VolcMessageFlags.NoSequence);
        var h2 = new VolcHeader(VolcMessageType.FullClientRequest, VolcMessageFlags.NoSequence);
        var h3 = new VolcHeader(VolcMessageType.AudioOnlyRequest, VolcMessageFlags.NoSequence);

        Assert.Equal(h1, h2);
        Assert.True(h1 == h2);
        Assert.NotEqual(h1, h3);
        Assert.True(h1 != h3);
    }
}
