using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Type4Me.Localization;
using Type4Me.Services;

namespace Type4Me.ViewModels;

/// <summary>
/// Vocabulary tab: hotwords + snippet replacements.
/// </summary>
public partial class VocabularyViewModel : ObservableObject
{
    [ObservableProperty] private string _hotwordText = string.Empty;
    [ObservableProperty] private ObservableCollection<SnippetItem> _snippets = [];
    [ObservableProperty] private string? _boostingTableID;

    public class SnippetItem : ObservableObject
    {
        private string _trigger = string.Empty;
        private string _value = string.Empty;

        public string Trigger { get => _trigger; set => SetProperty(ref _trigger, value); }
        public string Value { get => _value; set => SetProperty(ref _value, value); }
    }

    public VocabularyViewModel()
    {
        var hotwords = HotwordStorage.Load();
        HotwordText = string.Join("\n", hotwords);

        foreach (var s in SnippetStorage.Load())
            Snippets.Add(new SnippetItem { Trigger = s.Trigger, Value = s.Value });

        BoostingTableID = ASRCustomizationStorage.GetBoostingTableID();
    }

    [RelayCommand]
    public void SaveHotwords()
    {
        var words = HotwordText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        HotwordStorage.Save(words);
    }

    [RelayCommand]
    public void AddSnippet()
    {
        Snippets.Add(new SnippetItem());
    }

    [RelayCommand]
    public void RemoveSnippet(SnippetItem item)
    {
        Snippets.Remove(item);
        SaveSnippets();
    }

    [RelayCommand]
    public void SaveSnippets()
    {
        var snippets = Snippets
            .Where(s => !string.IsNullOrEmpty(s.Trigger))
            .Select(s => new SnippetStorage.Snippet(s.Trigger, s.Value))
            .ToArray();
        SnippetStorage.Save(snippets);
    }

    [RelayCommand]
    public void SaveBoostingSettings()
    {
        ASRCustomizationStorage.SetBoostingTableID(BoostingTableID);
    }
}
