namespace Type4Me.Database;

/// <summary>
/// A single recognition history record.
/// </summary>
public sealed class HistoryRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public double DurationSeconds { get; init; }
    public string RawText { get; init; } = string.Empty;
    public string? ProcessingMode { get; init; }
    public string? ProcessedText { get; init; }
    public string FinalText { get; init; } = string.Empty;
    public string Status { get; init; } = "completed";
}
