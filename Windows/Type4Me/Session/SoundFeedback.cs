using System.IO;
using System.Media;
using Type4Me.Audio;
using Type4Me.Services;

namespace Type4Me.Session;

/// <summary>
/// Synthesized audio feedback tones.
/// Uses programmatic WAV generation — no external sound files needed.
/// </summary>
public static class SoundFeedback
{
    private record ToneSpec(
        (double Frequency, double Duration)[] Tones,
        float Volume,
        string Label);

    private static bool _hasWarmedUp;
    private static readonly Dictionary<string, byte[]> _cachedWavs = new();

    private static readonly ToneSpec StartSpec = new(
        [(587, 0.06), (880, 0.09)],
        0.52f, "start");

    private static readonly ToneSpec StopSpec = new(
        [(740, 0.04), (1175, 0.06)],
        0.3f, "stop");

    private static readonly ToneSpec ErrorSpec = new(
        [(330, 0.08), (220, 0.1)],
        0.35f, "error");

    // ── Public API ─────────────────────────────────────────

    public static void WarmUp()
    {
        if (_hasWarmedUp) return;
        _hasWarmedUp = true;
        DebugFileLogger.Log("[SoundFeedback] warmUp");

        // Pre-generate all WAV data
        GetOrBuildWav(StartSpec);
        GetOrBuildWav(StopSpec);
        GetOrBuildWav(ErrorSpec);
    }

    public static void PlayStart()
    {
        DebugFileLogger.Log("[SoundFeedback] playStart");
        Play(StartSpec);
    }

    public static void PlayStop()
    {
        DebugFileLogger.Log("[SoundFeedback] playStop");
        Play(StopSpec);
    }

    public static void PlayError()
    {
        DebugFileLogger.Log("[SoundFeedback] playError");
        Play(ErrorSpec);
    }

    // ── Playback ───────────────────────────────────────────

    private static void Play(ToneSpec spec)
    {
        try
        {
            var wav = GetOrBuildWav(spec);
            using var ms = new MemoryStream(wav);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[SoundFeedback] {spec.Label} play failed: {ex.Message}");
            SystemSounds.Beep.Play();
        }
    }

    // ── Tone Synthesis ─────────────────────────────────────

    private static byte[] GetOrBuildWav(ToneSpec spec)
    {
        if (_cachedWavs.TryGetValue(spec.Label, out var cached))
            return cached;

        var wav = BuildToneData(spec);
        _cachedWavs[spec.Label] = wav;
        return wav;
    }

    private static byte[] BuildToneData(ToneSpec spec)
    {
        const int sampleRate = 44100;
        var samples = new List<short>();

        foreach (var (frequency, duration) in spec.Tones)
        {
            int frameCount = (int)(duration * sampleRate);
            for (int i = 0; i < frameCount; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = Math.Sin(Math.PI * t / duration);
                double value = Math.Sin(2.0 * Math.PI * frequency * t) * envelope * 0.5 * spec.Volume;
                samples.Add((short)(value * 32767));
            }
        }

        return WavEncoder.EncodeFromSamples(samples.ToArray(), sampleRate);
    }
}
