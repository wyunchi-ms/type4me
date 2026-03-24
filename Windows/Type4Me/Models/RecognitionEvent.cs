using Type4Me.Localization;

namespace Type4Me.Models;

/// <summary>
/// Live ASR transcript state — confirmed segments + partial text.
/// </summary>
public sealed class RecognitionTranscript
{
    /// <summary>Definite (final) utterances from ASR.</summary>
    public string[] ConfirmedSegments { get; init; } = [];

    /// <summary>Current unconfirmed partial text.</summary>
    public string PartialText { get; init; } = string.Empty;

    /// <summary>Server's full-text result (may differ from segments).</summary>
    public string AuthoritativeText { get; init; } = string.Empty;

    /// <summary>True on the final async response.</summary>
    public bool IsFinal { get; init; }

    /// <summary>Joined confirmed segments + partial.</summary>
    public string ComposedText => string.Join("", ConfirmedSegments) + PartialText;

    /// <summary>Authoritative text if available, otherwise composed text.</summary>
    public string DisplayText =>
        !string.IsNullOrEmpty(AuthoritativeText) ? AuthoritativeText : ComposedText;
}

/// <summary>
/// Outcome of text injection into the focused UI element.
/// </summary>
public enum InjectionOutcome
{
    /// <summary>Text was pasted into the active field.</summary>
    Inserted,

    /// <summary>No editable field found — text only copied to clipboard.</summary>
    CopiedToClipboard,
}

public static class InjectionOutcomeExtensions
{
    public static string CompletionMessage(this InjectionOutcome outcome) => outcome switch
    {
        InjectionOutcome.Inserted => Loc.L("已完成", "Done"),
        InjectionOutcome.CopiedToClipboard => Loc.L("已复制到剪贴板", "Copied to clipboard"),
        _ => Loc.L("已完成", "Done"),
    };
}

/// <summary>
/// Events emitted by a speech recognizer during a session.
/// </summary>
public abstract record RecognitionEvent
{
    /// <summary>Audio flowing, UI can show recording state.</summary>
    public sealed record Ready : RecognitionEvent;

    /// <summary>Live ASR update.</summary>
    public sealed record Transcript(RecognitionTranscript Value) : RecognitionEvent;

    /// <summary>ASR/connection error.</summary>
    public sealed record Error(Exception Exception) : RecognitionEvent;

    /// <summary>ASR stream ended normally.</summary>
    public sealed record Completed : RecognitionEvent;

    /// <summary>LLM result available.</summary>
    public sealed record ProcessingResult(string Text) : RecognitionEvent;

    /// <summary>Final text injected.</summary>
    public sealed record Finalized(string Text, InjectionOutcome Injection) : RecognitionEvent;
}

/// <summary>
/// LLM connection configuration.
/// </summary>
public sealed class LLMConfig
{
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string BaseURL { get; init; } = string.Empty;

    /// <summary>
    /// API version string (used by Azure OpenAI). Null for other providers.
    /// </summary>
    public string? ApiVersion { get; init; }

    public LLMConfig() { }

    public LLMConfig(string apiKey, string model, string baseURL, string? apiVersion = null)
    {
        ApiKey = apiKey;
        Model = model;
        BaseURL = baseURL;
        ApiVersion = apiVersion;
    }
}
