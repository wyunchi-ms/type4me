using System.Windows;
using System.Windows.Controls;
using Type4Me.Localization;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// Settings window with sidebar navigation and 5 tabs.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow()
    {
        _vm = new SettingsViewModel();
        DataContext = this; // Window-level bindings for nav labels
        InitializeComponent();

        TabGeneral.DataContext = _vm.General;
        TabVocabulary.DataContext = _vm.Vocabulary;
        TabModes.DataContext = _vm.Modes;
        TabHistory.DataContext = _vm.History;
        TabAbout.DataContext = _vm.About;
    }

    // ── Localized tab labels ─────────────────────────────
    public string GeneralTabLabel => Loc.L("通用", "General");
    public string VocabularyTabLabel => Loc.L("词汇", "Vocabulary");
    public string ModesTabLabel => Loc.L("模式", "Modes");
    public string HistoryTabLabel => Loc.L("历史", "History");
    public string AboutTabLabel => Loc.L("关于", "About");

    // ── Tab navigation ───────────────────────────────────
    private void ShowTab(UIElement tab)
    {
        // Guard: called during InitializeComponent before tabs are created
        if (TabGeneral == null) return;

        TabGeneral.Visibility = Visibility.Collapsed;
        TabVocabulary.Visibility = Visibility.Collapsed;
        TabModes.Visibility = Visibility.Collapsed;
        TabHistory.Visibility = Visibility.Collapsed;
        TabAbout.Visibility = Visibility.Collapsed;
        tab.Visibility = Visibility.Visible;
    }

    private void NavGeneral_Checked(object sender, RoutedEventArgs e) => ShowTab(TabGeneral);
    private void NavVocabulary_Checked(object sender, RoutedEventArgs e) => ShowTab(TabVocabulary);
    private void NavModes_Checked(object sender, RoutedEventArgs e) => ShowTab(TabModes);
    private void NavHistory_Checked(object sender, RoutedEventArgs e) => ShowTab(TabHistory);
    private void NavAbout_Checked(object sender, RoutedEventArgs e) => ShowTab(TabAbout);
}
