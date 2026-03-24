namespace Type4Me.Protocol;

/// <summary>
/// Volcengine binary protocol message types.
/// </summary>
public enum VolcMessageType : byte
{
    FullClientRequest = 0b0001,
    AudioOnlyRequest  = 0b0010,
    ServerResponse    = 0b1001,
    ServerError       = 0b1111,
}

/// <summary>
/// Volcengine binary protocol message flags.
/// </summary>
public enum VolcMessageFlags : byte
{
    NoSequence           = 0b0000,
    PositiveSequence     = 0b0001,
    LastPacketNoSequence = 0b0010,
    NegativeSequenceLast = 0b0011,
    AsyncFinal           = 0b0100,
}

/// <summary>
/// Volcengine binary protocol serialization format.
/// </summary>
public enum VolcSerialization : byte
{
    None = 0b0000,
    Json = 0b0001,
}

/// <summary>
/// Volcengine binary protocol compression format.
/// </summary>
public enum VolcCompression : byte
{
    None = 0b0000,
    Gzip = 0b0001,
}

/// <summary>
/// 4-byte binary header for the Volcengine streaming ASR protocol.
/// Byte layout: [version:4|headerSize:4] [msgType:4|flags:4] [serialization:4|compression:4] [reserved]
/// </summary>
public struct VolcHeader : IEquatable<VolcHeader>
{
    public byte Version;
    public byte HeaderSize;
    public VolcMessageType MessageType;
    public VolcMessageFlags Flags;
    public VolcSerialization Serialization;
    public VolcCompression Compression;
    public byte Reserved;

    public VolcHeader(
        VolcMessageType messageType,
        VolcMessageFlags flags,
        VolcSerialization serialization = VolcSerialization.None,
        VolcCompression compression = VolcCompression.None)
    {
        Version = 0b0001;
        HeaderSize = 0b0001; // 1 unit = 4 bytes
        MessageType = messageType;
        Flags = flags;
        Serialization = serialization;
        Compression = compression;
        Reserved = 0x00;
    }

    /// <summary>Encode to 4 bytes.</summary>
    public byte[] Encode()
    {
        var data = new byte[4];
        data[0] = (byte)((Version << 4) | (HeaderSize & 0x0F));
        data[1] = (byte)(((byte)MessageType << 4) | ((byte)Flags & 0x0F));
        data[2] = (byte)(((byte)Serialization << 4) | ((byte)Compression & 0x0F));
        data[3] = Reserved;
        return data;
    }

    /// <summary>Decode from 4+ bytes.</summary>
    public static VolcHeader Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            throw new VolcProtocolException("Header too short");

        byte byte0 = data[0];
        byte byte1 = data[1];
        byte byte2 = data[2];
        byte byte3 = data[3];

        byte version    = (byte)((byte0 >> 4) & 0x0F);
        byte headerSize = (byte)(byte0 & 0x0F);
        byte msgTypeRaw = (byte)((byte1 >> 4) & 0x0F);
        byte flagsRaw   = (byte)(byte1 & 0x0F);
        byte serRaw     = (byte)((byte2 >> 4) & 0x0F);
        byte compRaw    = (byte)(byte2 & 0x0F);

        if (!Enum.IsDefined(typeof(VolcMessageType), msgTypeRaw))
            throw new VolcProtocolException($"Unknown message type: 0x{msgTypeRaw:X}");
        if (!Enum.IsDefined(typeof(VolcMessageFlags), flagsRaw))
            throw new VolcProtocolException($"Unknown flags: 0x{flagsRaw:X}");
        if (!Enum.IsDefined(typeof(VolcSerialization), serRaw))
            throw new VolcProtocolException($"Unknown serialization: 0x{serRaw:X}");
        if (!Enum.IsDefined(typeof(VolcCompression), compRaw))
            throw new VolcProtocolException($"Unknown compression: 0x{compRaw:X}");

        return new VolcHeader
        {
            Version = version,
            HeaderSize = headerSize,
            MessageType = (VolcMessageType)msgTypeRaw,
            Flags = (VolcMessageFlags)flagsRaw,
            Serialization = (VolcSerialization)serRaw,
            Compression = (VolcCompression)compRaw,
            Reserved = byte3,
        };
    }

    /// <summary>Whether the header is followed by a 4-byte sequence number.</summary>
    public readonly bool HasSequence =>
        Flags == VolcMessageFlags.PositiveSequence || Flags == VolcMessageFlags.NegativeSequenceLast;

    public readonly bool Equals(VolcHeader other) =>
        Version == other.Version && HeaderSize == other.HeaderSize &&
        MessageType == other.MessageType && Flags == other.Flags &&
        Serialization == other.Serialization && Compression == other.Compression;

    public override readonly bool Equals(object? obj) => obj is VolcHeader h && Equals(h);
    public override readonly int GetHashCode() =>
        HashCode.Combine(Version, HeaderSize, MessageType, Flags, Serialization, Compression);

    public static bool operator ==(VolcHeader left, VolcHeader right) => left.Equals(right);
    public static bool operator !=(VolcHeader left, VolcHeader right) => !left.Equals(right);
}

/// <summary>
/// Volcengine protocol-specific exceptions.
/// </summary>
public class VolcProtocolException : Exception
{
    public int? ErrorCode { get; }

    public VolcProtocolException(string message, int? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}
