namespace Type4Me.Models;

/// <summary>
/// Optional runtime context sent with an LLM request.
/// </summary>
public sealed class LLMRequestContext
{
    public string CurrentApplicationName { get; init; } = string.Empty;
    public string? CurrentApplicationScreenshotBase64 { get; init; }
    public string CurrentApplicationScreenshotMediaType { get; init; } = "image/png";

    public bool HasScreenshot => !string.IsNullOrWhiteSpace(CurrentApplicationScreenshotBase64);

    public string ScreenshotDataUrl => HasScreenshot
        ? $"data:{CurrentApplicationScreenshotMediaType};base64,{CurrentApplicationScreenshotBase64}"
        : string.Empty;
}
