using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Type4Me.Localization;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.ViewModels;

/// <summary>
/// Modes settings tab: view and edit processing modes.
/// </summary>
public partial class ModesSettingsViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ProcessingMode> _modes = [];
    [ObservableProperty] private ProcessingMode? _selectedMode;

    private readonly ModeStorage _storage = new();

    public ModesSettingsViewModel()
    {
        var modes = _storage.Load();
        Modes = new ObservableCollection<ProcessingMode>(modes);
    }

    [RelayCommand]
    public void AddMode()
    {
        var mode = new ProcessingMode
        {
            Id = Guid.NewGuid(),
            Name = Loc.L("新模式", "New Mode"),
            Prompt = "",
            IsBuiltin = false,
        };
        Modes.Add(mode);
        SelectedMode = mode;
        Save();
    }

    [RelayCommand]
    public void DeleteMode(ProcessingMode mode)
    {
        if (mode.IsBuiltin) return;
        Modes.Remove(mode);
        Save();
    }

    public void Save()
    {
        _storage.Save(Modes.ToArray());
    }
}
