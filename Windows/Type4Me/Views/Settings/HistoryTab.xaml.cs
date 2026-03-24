using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Type4Me.Database;
using Type4Me.Localization;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// History tab: paginated history with search and deletion.
/// </summary>
public partial class HistoryTab : UserControl
{
    private HistoryViewModel? _vm;

    public HistoryTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is HistoryViewModel vm)
        {
            _vm = vm;
            SetupUI();
            BindRecords();
        }
    }

    private void SetupUI()
    {
        // Localized labels
        HistoryTitle.Text = Loc.L("历史记录", "History");
        DeleteAllBtn.Content = Loc.L("清空全部", "Delete All");
        SearchBtn.Content = Loc.L("搜索", "Search");
        LoadMoreBtn.Content = Loc.L("加载更多", "Load More");
        SearchBox.Text = _vm?.SearchText ?? "";
    }

    private void BindRecords()
    {
        if (_vm == null) return;
        RecordsListView.ItemsSource = _vm.Records;
        UpdateCountLabel();
    }

    private void UpdateCountLabel()
    {
        CountLabel.Text = $"({_vm?.TotalCount ?? 0})";
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.SearchText = SearchBox.Text;
        await _vm.SearchAsync();
        UpdateCountLabel();
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm != null)
        {
            _vm.SearchText = SearchBox.Text;
            await _vm.SearchAsync();
            UpdateCountLabel();
        }
    }

    private async void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
            await _vm.LoadMoreAsync();
    }

    private async void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryRecord record && _vm != null)
        {
            await _vm.DeleteAsync(record);
            UpdateCountLabel();
        }
    }

    private async void DeleteAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            Loc.L("确定要删除所有历史记录吗？此操作不可撤销。",
                   "Are you sure you want to delete all history? This cannot be undone."),
            "Type4Me",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && _vm != null)
        {
            await _vm.DeleteAllAsync();
            UpdateCountLabel();
        }
    }
}
