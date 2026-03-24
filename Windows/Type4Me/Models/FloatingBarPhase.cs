namespace Type4Me.Models;

/// <summary>
/// Floating bar display phase — drives the capsule bar's visual state.
/// </summary>
public enum FloatingBarPhase
{
    Hidden,
    Preparing,
    Recording,
    Processing,
    Done,
    Error,
}
