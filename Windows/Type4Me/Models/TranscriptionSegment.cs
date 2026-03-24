namespace Type4Me.Models;

/// <summary>
/// A single segment of live transcription.
/// </summary>
public sealed class TranscriptionSegment
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Text { get; }
    public bool IsConfirmed { get; }

    public TranscriptionSegment(string text, bool isConfirmed)
    {
        Text = text;
        IsConfirmed = isConfirmed;
    }
}
