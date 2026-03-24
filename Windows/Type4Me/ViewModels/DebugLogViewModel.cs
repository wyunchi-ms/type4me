using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Type4Me.ViewModels;

/// <summary>
/// ViewModel for the real-time debug console window.
/// Captures all pipeline events: hotkey, ASR, LLM, injection.
/// </summary>
public partial class DebugLogViewModel : ObservableObject
{
    public ObservableCollection<DebugLogEntry> Entries { get; } = [];

    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private int _maxEntries = 500;

    public void Add(string category, string message, string? detail = null)
    {
        var entry = new DebugLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            Detail = detail,
        };

        // Must be called on UI thread
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
        {
            AddEntry(entry);
        }
        else
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => AddEntry(entry));
        }
    }

    private void AddEntry(DebugLogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(0);
    }

    public void Clear() => Entries.Clear();
}

public class DebugLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Category { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Detail { get; init; }

    public string TimeString => Timestamp.ToString("HH:mm:ss.fff");

    public string CategoryColor => Category switch
    {
        "HOTKEY" => "#2196F3",
        "ASR" => "#4CAF50",
        "LLM" => "#FF9800",
        "INJECT" => "#9C27B0",
        "ERROR" => "#F44336",
        "SESSION" => "#607D8B",
        "AUDIO" => "#00BCD4",
        _ => "#6B6B6B",
    };
}
