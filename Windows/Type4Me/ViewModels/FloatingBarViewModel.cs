using CommunityToolkit.Mvvm.ComponentModel;
using Type4Me.Models;

namespace Type4Me.ViewModels;

/// <summary>
/// ViewModel for the floating bar overlay.
/// </summary>
public partial class FloatingBarViewModel : ObservableObject
{
    [ObservableProperty] private FloatingBarPhase _phase = FloatingBarPhase.Hidden;
    [ObservableProperty] private string _transcriptionText = string.Empty;
    [ObservableProperty] private string _feedbackMessage = "Done";
    [ObservableProperty] private string _processingLabel = "Processing";
    [ObservableProperty] private double _audioLevel;
    [ObservableProperty] private DateTime? _recordingStartDate;
    [ObservableProperty] private List<TranscriptionSegment> _segments = [];

    public void StartRecording()
    {
        Segments = [];
        AudioLevel = 0;
        RecordingStartDate = null;
        FeedbackMessage = Localization.Loc.L("已完成", "Done");
        Phase = FloatingBarPhase.Preparing;
    }

    public void MarkRecordingReady()
    {
        if (Phase != FloatingBarPhase.Preparing) return;
        AudioLevel = 0;
        RecordingStartDate = DateTime.Now;
        Phase = FloatingBarPhase.Recording;
    }

    public void StopRecording()
    {
        switch (Phase)
        {
            case FloatingBarPhase.Preparing:
                Cancel();
                break;
            case FloatingBarPhase.Recording:
                Phase = FloatingBarPhase.Processing;
                break;
        }
    }

    public void SetLiveTranscript(RecognitionTranscript transcript)
    {
        if (transcript.IsFinal && !string.IsNullOrEmpty(transcript.AuthoritativeText)
            && transcript.AuthoritativeText != transcript.ComposedText)
        {
            Segments = [new TranscriptionSegment(transcript.AuthoritativeText, true)];
            UpdateTranscriptionText();
            return;
        }

        var segs = new List<TranscriptionSegment>();
        foreach (var s in transcript.ConfirmedSegments)
            segs.Add(new TranscriptionSegment(s, true));
        if (!string.IsNullOrEmpty(transcript.PartialText))
            segs.Add(new TranscriptionSegment(transcript.PartialText, false));

        Segments = segs;
        UpdateTranscriptionText();
    }

    public void ShowProcessingResult(string result)
    {
        if (string.IsNullOrEmpty(result)) { Cancel(); return; }
        Segments = [new TranscriptionSegment(result, true)];
        UpdateTranscriptionText();
    }

    public void Finalize(string text, InjectionOutcome outcome)
    {
        if (string.IsNullOrEmpty(text)) { Cancel(); return; }
        Segments = [new TranscriptionSegment(text, true)];
        UpdateTranscriptionText();
        ShowDone(outcome.CompletionMessage());
    }

    public void ShowError(string message)
    {
        FeedbackMessage = message;
        AudioLevel = 0;
        RecordingStartDate = null;
        Phase = FloatingBarPhase.Error;
        ScheduleAutoHide(FloatingBarPhase.Error, 1800);
    }

    public void Cancel()
    {
        Phase = FloatingBarPhase.Hidden;
        Segments = [];
        AudioLevel = 0;
    }

    private void ShowDone(string message = "Done")
    {
        FeedbackMessage = message;
        Phase = FloatingBarPhase.Done;
        ScheduleAutoHide(FloatingBarPhase.Done, 1500);
    }

    private void ScheduleAutoHide(FloatingBarPhase expectedPhase, int delayMs)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            if (Phase == expectedPhase)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (Phase == expectedPhase)
                        Phase = FloatingBarPhase.Hidden;
                });
            }
        });
    }

    private void UpdateTranscriptionText()
    {
        TranscriptionText = string.Join("", Segments.Select(s => s.Text));
    }
}
