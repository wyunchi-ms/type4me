using System.Threading.Channels;
using Type4Me.Models;

namespace Type4Me.ASR;

/// <summary>
/// Interface for speech recognition clients.
/// Each ASR provider implements this to connect, send audio, and emit events.
/// </summary>
public interface ISpeechRecognizer : IDisposable
{
    /// <summary>Connect to the ASR service.</summary>
    Task ConnectAsync(Dictionary<string, string> credentials, ASRRequestOptions options, CancellationToken ct = default);

    /// <summary>Send a chunk of PCM audio data.</summary>
    Task SendAudioAsync(byte[] data, CancellationToken ct = default);

    /// <summary>Signal end of audio stream.</summary>
    Task EndAudioAsync(CancellationToken ct = default);

    /// <summary>Disconnect from the ASR service.</summary>
    Task DisconnectAsync();

    /// <summary>Channel reader for recognition events.</summary>
    ChannelReader<RecognitionEvent> Events { get; }
}
