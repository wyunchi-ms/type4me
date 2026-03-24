using CommunityToolkit.Mvvm.ComponentModel;
using Type4Me.Input;
using Type4Me.Models;
using Type4Me.Services;
using Type4Me.Session;

namespace Type4Me.ViewModels;

/// <summary>
/// Root application ViewModel — coordinates session, hotkeys, and UI state.
/// </summary>
public partial class AppViewModel : ObservableObject
{
    public FloatingBarViewModel FloatingBar { get; } = new();
    public RecognitionSession Session { get; } = new();
    public HotkeyManager HotkeyManager { get; } = new();
    public DebugLogViewModel DebugLog { get; } = new();

    [ObservableProperty] private ProcessingMode _currentMode;
    [ObservableProperty] private ProcessingMode[] _availableModes = [];
    [ObservableProperty] private bool _hasCompletedSetup;

    public AppViewModel()
    {
        var storage = new ModeStorage();
        var modes = storage.Load();
        AvailableModes = modes;
        CurrentMode = modes.FirstOrDefault(m => m.Id == ProcessingMode.SmartDirectId)
            ?? modes.FirstOrDefault()
            ?? ProcessingMode.Direct;

        HasCompletedSetup = SettingsService.GetBool("hasCompletedSetup");

        // Wire session events → floating bar
        Session.OnASREvent = evt =>
        {
            LogASREvent(evt);
            System.Windows.Application.Current?.Dispatcher.Invoke(() => HandleASREvent(evt));
        };

        Session.OnDebugLog = (category, message, detail) =>
        {
            DebugLog.Add(category, message, detail);
        };

        Session.OnAudioLevel = level =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                FloatingBar.AudioLevel = level;
            });
        };

        // Wire hotkeys
        HotkeyManager.SetModes(AvailableModes);
        HotkeyManager.OnStartRecording += mode =>
        {
            CurrentMode = mode;
            DebugLog.Add("HOTKEY", $"▶ {mode.Name}");
            _ = Task.Run(async () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    FloatingBar.ProcessingLabel = mode.ProcessingLabel;
                    FloatingBar.StartRecording();
                });
                await Session.StartRecordingAsync(mode);
                HotkeyManager.SetRecording(true, mode);
            });
        };

        HotkeyManager.OnStopRecording += mode =>
        {
            DebugLog.Add("HOTKEY", $"■ {mode.Name}");
            _ = Task.Run(async () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    FloatingBar.StopRecording();
                });
                HotkeyManager.SetRecording(false);
                await Session.StopRecordingAsync();
            });
        };

        HotkeyManager.OnCrossModeStop += newMode =>
        {
            Session.SwitchMode(newMode);
            _ = Task.Run(async () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    FloatingBar.StopRecording();
                });
                HotkeyManager.SetRecording(false);
                await Session.StopRecordingAsync();
            });
        };
    }

    private void HandleASREvent(RecognitionEvent evt)
    {
        switch (evt)
        {
            case RecognitionEvent.Ready:
                FloatingBar.MarkRecordingReady();
                break;

            case RecognitionEvent.Transcript t:
                FloatingBar.SetLiveTranscript(t.Value);
                break;

            case RecognitionEvent.ProcessingResult pr:
                FloatingBar.ShowProcessingResult(pr.Text);
                break;

            case RecognitionEvent.Finalized f:
                FloatingBar.Finalize(f.Text, f.Injection);
                break;

            case RecognitionEvent.Error e:
                FloatingBar.ShowError(e.Exception.Message);
                break;

            case RecognitionEvent.Completed:
                // Handled by other events
                break;
        }
    }

    public void Start()
    {
        Session.WarmUp();
        HotkeyManager.Install();
        SoundFeedback.WarmUp();
    }

    public void Stop()
    {
        HotkeyManager.Dispose();
        Session.Dispose();
    }

    /// <summary>Reload modes after settings or wizard changes.</summary>
    public void ReloadModes()
    {
        var storage = new ModeStorage();
        var modes = storage.Load();
        AvailableModes = modes;
        HotkeyManager.SetModes(modes);

        // Re-select current mode if it still exists
        var existing = modes.FirstOrDefault(m => m.Id == CurrentMode.Id);
        if (existing != null)
            CurrentMode = existing;
        else
            CurrentMode = modes.FirstOrDefault(m => m.Id == ProcessingMode.SmartDirectId)
                ?? modes.FirstOrDefault()
                ?? ProcessingMode.Direct;
    }

    private void LogASREvent(RecognitionEvent evt)
    {
        switch (evt)
        {
            case RecognitionEvent.Ready:
                DebugLog.Add("SESSION", Localization.Loc.L("录音开始", "Recording started"));
                break;

            case RecognitionEvent.Finalized f:
                DebugLog.Add("INJECT", f.Injection.CompletionMessage(), f.Text);
                break;

            case RecognitionEvent.Error e:
                DebugLog.Add("ERROR", e.Exception.Message, e.Exception.GetType().Name);
                break;
        }
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        return text.Length <= max ? text : text[..max] + "…";
    }
}
