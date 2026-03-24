using CommunityToolkit.Mvvm.ComponentModel;

namespace Type4Me.ViewModels;

/// <summary>
/// Settings window ViewModel — manages tab navigation.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public enum SettingsTab { General, Vocabulary, Modes, History, About }

    [ObservableProperty] private SettingsTab _currentTab = SettingsTab.General;

    public GeneralSettingsViewModel General { get; } = new();
    public VocabularyViewModel Vocabulary { get; } = new();
    public ModesSettingsViewModel Modes { get; } = new();
    public HistoryViewModel History { get; } = new();
    public AboutViewModel About { get; } = new();
}
