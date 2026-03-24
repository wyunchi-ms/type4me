using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Type4Me.Database;
using Type4Me.Localization;

namespace Type4Me.ViewModels;

/// <summary>
/// History tab ViewModel.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<HistoryRecord> _records = [];
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;

    private HistoryStore? _store;
    private int _offset;
    private const int PageSize = 50;

    public HistoryViewModel()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                _store = new HistoryStore();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Services.DebugFileLogger.Log($"[HistoryVM] Init/Load error: {ex.Message}");
            }
        });
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_store == null) return;
        _offset = 0;
        var items = await _store.FetchAllAsync(PageSize, 0,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);
        Records = new ObservableCollection<HistoryRecord>(items);
        TotalCount = await _store.CountAsync();
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (_store == null) return;
        _offset += PageSize;
        var items = await _store.FetchAllAsync(PageSize, _offset,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);
        foreach (var item in items)
            Records.Add(item);
    }

    [RelayCommand]
    public async Task DeleteAsync(HistoryRecord record)
    {
        if (_store == null) return;
        await _store.DeleteAsync(record.Id);
        Records.Remove(record);
        TotalCount--;
    }

    [RelayCommand]
    public async Task DeleteAllAsync()
    {
        if (_store == null) return;
        await _store.DeleteAllAsync();
        Records.Clear();
        TotalCount = 0;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        await LoadAsync();
    }
}
