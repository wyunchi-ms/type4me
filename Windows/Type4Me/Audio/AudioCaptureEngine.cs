using NAudio.Wave;
using Type4Me.Services;

namespace Type4Me.Audio;

/// <summary>
/// Audio capture engine using NAudio WaveInEvent.
/// Produces 16kHz / 16-bit / mono PCM chunks (200ms = 6400 bytes).
/// </summary>
public sealed class AudioCaptureEngine : IDisposable
{
    // ── Constants ───────────────────────────────────────────

    public const int SampleRate = 16000;
    public const int Channels = 1;
    public const int BitsPerSample = 16;
    public const int ChunkDurationMs = 200;
    public const int SamplesPerChunk = 3200;
    public const int ChunkByteSize = 6400;

    // ── Events ─────────────────────────────────────────────

    /// <summary>Called on the NAudio thread with each 6400-byte PCM chunk.</summary>
    public Action<byte[]>? OnAudioChunk;

    /// <summary>Called with normalized 0..1 audio level for visualization.</summary>
    public Action<float>? OnAudioLevel;

    // ── Private state ──────────────────────────────────────

    private WaveInEvent? _waveIn;
    private readonly object _lock = new();
    private byte[] _buffer = [];
    private byte[] _accumulated = [];
    private int _levelCounter;
    private bool _isWarmedUp;

    // ── Warm-up ────────────────────────────────────────────

    /// <summary>Pre-initialize audio subsystem to reduce first-recording latency.</summary>
    public void WarmUp()
    {
        if (_isWarmedUp) return;

        Task.Run(() =>
        {
            try
            {
                var waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                    BufferMilliseconds = 100,
                };
                waveIn.StartRecording();
                Thread.Sleep(300);
                waveIn.StopRecording();
                waveIn.Dispose();
                _isWarmedUp = true;
                DebugFileLogger.Log("[Audio] Warm-up complete");
            }
            catch (Exception ex)
            {
                DebugFileLogger.Log($"[Audio] Warm-up failed: {ex.Message}");
            }
        });
    }

    // ── Start / Stop ───────────────────────────────────────

    public void Start()
    {
        lock (_lock)
        {
            _buffer = [];
            _accumulated = [];
        }
        _levelCounter = 0;

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
            BufferMilliseconds = 100,
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _isWarmedUp = true;
        DebugFileLogger.Log("[Audio] Capture started");
    }

    public void Stop()
    {
        if (_waveIn == null) return;

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _waveIn = null;

        FlushRemaining();
        _levelCounter = 0;
        DebugFileLogger.Log("[Audio] Capture stopped");
    }

    /// <summary>Returns all PCM audio accumulated since Start().</summary>
    public byte[] GetRecordedAudio()
    {
        lock (_lock)
        {
            return _accumulated.ToArray();
        }
    }

    // ── NAudio callback ────────────────────────────────────

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        // Audio level (~20 times/sec)
        _levelCounter++;
        if (_levelCounter % 3 == 0)
        {
            float level = CalculateLevel(e.Buffer, e.BytesRecorded);
            OnAudioLevel?.Invoke(level);
        }

        lock (_lock)
        {
            // Append to accumulated and chunk buffer
            int prevAccLen = _accumulated.Length;
            Array.Resize(ref _accumulated, prevAccLen + e.BytesRecorded);
            Buffer.BlockCopy(e.Buffer, 0, _accumulated, prevAccLen, e.BytesRecorded);

            int prevBufLen = _buffer.Length;
            Array.Resize(ref _buffer, prevBufLen + e.BytesRecorded);
            Buffer.BlockCopy(e.Buffer, 0, _buffer, prevBufLen, e.BytesRecorded);

            EmitFullChunks();
        }
    }

    /// <summary>Emit all complete 6400-byte chunks from buffer. Must be called with _lock held.</summary>
    private void EmitFullChunks()
    {
        while (_buffer.Length >= ChunkByteSize)
        {
            var chunk = new byte[ChunkByteSize];
            Buffer.BlockCopy(_buffer, 0, chunk, 0, ChunkByteSize);

            int remaining = _buffer.Length - ChunkByteSize;
            if (remaining > 0)
            {
                var newBuf = new byte[remaining];
                Buffer.BlockCopy(_buffer, ChunkByteSize, newBuf, 0, remaining);
                _buffer = newBuf;
            }
            else
            {
                _buffer = [];
            }

            OnAudioChunk?.Invoke(chunk);
        }
    }

    /// <summary>RMS → normalized 0..1 level from 16-bit PCM.</summary>
    private static float CalculateLevel(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 2;
        if (sampleCount == 0) return 0;

        double sum = 0;
        int stride = Math.Max(1, sampleCount / 256);
        int count = 0;

        for (int i = 0; i < sampleCount; i += stride)
        {
            short sample = BitConverter.ToInt16(buffer, i * 2);
            double normalized = sample / 32768.0;
            sum += normalized * normalized;
            count++;
        }

        float rms = (float)Math.Sqrt(sum / count);
        float db = 20f * MathF.Log10(MathF.Max(rms, 1e-7f));
        // Map -50dB..0dB → 0..1
        return Math.Clamp((db + 50f) / 50f, 0f, 1f);
    }

    private void FlushRemaining()
    {
        byte[] remaining;
        lock (_lock)
        {
            remaining = _buffer;
            _buffer = [];
        }

        if (remaining.Length > 0)
            OnAudioChunk?.Invoke(remaining);
    }

    public void Dispose()
    {
        Stop();
    }
}
