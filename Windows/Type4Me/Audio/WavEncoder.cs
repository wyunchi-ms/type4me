namespace Type4Me.Audio;

/// <summary>
/// Encodes raw 16-bit PCM data into a WAV byte array.
/// </summary>
public static class WavEncoder
{
    /// <summary>
    /// Wrap raw PCM data in a WAV container.
    /// </summary>
    public static byte[] Encode(byte[] pcmData, int sampleRate = 16000, int channels = 1, int bitsPerSample = 16)
    {
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize = pcmData.Length;
        int fileSize = 36 + dataSize;

        using var ms = new System.IO.MemoryStream(44 + dataSize);
        using var bw = new System.IO.BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(fileSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);                  // chunk size
        bw.Write((short)1);            // PCM format
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);

        // data chunk
        bw.Write("data"u8);
        bw.Write(dataSize);
        bw.Write(pcmData);

        return ms.ToArray();
    }

    /// <summary>
    /// Encode Int16 samples into a WAV byte array.
    /// </summary>
    public static byte[] EncodeFromSamples(short[] samples, int sampleRate = 44100)
    {
        var pcm = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
        return Encode(pcm, sampleRate);
    }
}
