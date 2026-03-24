using System.Windows;
using System.Windows.Controls;
using Type4Me.Localization;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// Vocabulary tab: hotwords and snippet replacements.
/// </summary>
public partial class VocabularyTab : UserControl
{
    private VocabularyViewModel? _vm;

    public VocabularyTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is VocabularyViewModel vm)
        {
            _vm = vm;
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (_vm == null) return;

        // Localized labels
        HotwordsTitle.Text = Loc.L("热词", "Hotwords");
        HotwordsDesc.Text = Loc.L(
            "每行一个热词，用于提升语音识别准确度。",
            "One hotword per line to improve speech recognition accuracy.");
        SaveHotwordsBtn.Content = Loc.L("保存热词", "Save Hotwords");
        SnippetsTitle.Text = Loc.L("文本替换", "Snippets");
        SnippetsDesc.Text = Loc.L(
            "识别结果中匹配触发词时自动替换为指定文本。",
            "Auto-replace trigger words in recognition results.");
        AddSnippetBtn.Content = $"+ {Loc.L("添加", "Add")}";
        TriggerHeader.Text = Loc.L("触发词", "Trigger");
        ValueHeader.Text = Loc.L("替换文本", "Replacement");
        BoostingTitle.Text = Loc.L("增强表 ID", "Boosting Table ID");
        BoostingDesc.Text = Loc.L(
            "火山引擎 ASR 自定义增强表 ID（可选）。",
            "Volcano ASR custom boosting table ID (optional).");
        SaveBoostingBtn.Content = Loc.L("保存", "Save");

        // Bind data
        HotwordTextBox.Text = _vm.HotwordText;
        BoostingIdBox.Text = _vm.BoostingTableID ?? "";

        RenderSnippets();
    }

    private void RenderSnippets()
    {
        if (_vm == null) return;
        SnippetsList.Items.Clear();

        foreach (var snippet in _vm.Snippets)
        {
            var row = CreateSnippetRow(snippet);
            SnippetsList.Items.Add(row);
        }
    }

    private Grid CreateSnippetRow(VocabularyViewModel.SnippetItem snippet)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

        var triggerBox = new TextBox
        {
            Text = snippet.Trigger,
            FontSize = 12,
        };
        triggerBox.TextChanged += (_, _) =>
        {
            snippet.Trigger = triggerBox.Text;
            _vm?.SaveSnippetsCommand.Execute(null);
        };
        Grid.SetColumn(triggerBox, 0);
        grid.Children.Add(triggerBox);

        var valueBox = new TextBox
        {
            Text = snippet.Value,
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
        };
        valueBox.TextChanged += (_, _) =>
        {
            snippet.Value = valueBox.Text;
            _vm?.SaveSnippetsCommand.Execute(null);
        };
        Grid.SetColumn(valueBox, 1);
        grid.Children.Add(valueBox);

        var deleteBtn = new Button
        {
            Content = "×",
            FontSize = 14,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(204, 71, 56)),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        deleteBtn.Click += (_, _) =>
        {
            _vm?.RemoveSnippetCommand.Execute(snippet);
            RenderSnippets();
        };
        Grid.SetColumn(deleteBtn, 2);
        grid.Children.Add(deleteBtn);

        return grid;
    }

    // ── Event Handlers ───────────────────────────────────

    private void SaveHotwords_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.HotwordText = HotwordTextBox.Text;
        _vm.SaveHotwordsCommand.Execute(null);
    }

    private void AddSnippet_Click(object sender, RoutedEventArgs e)
    {
        _vm?.AddSnippetCommand.Execute(null);
        RenderSnippets();
    }

    private void SaveBoosting_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.BoostingTableID = string.IsNullOrWhiteSpace(BoostingIdBox.Text) ? null : BoostingIdBox.Text.Trim();
        _vm.SaveBoostingSettingsCommand.Execute(null);
    }
}
