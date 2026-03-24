using System.Windows;
using System.Windows.Controls;
using Type4Me.Input;
using Type4Me.Localization;
using Type4Me.Models;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// Modes settings tab: view and edit processing modes.
/// </summary>
public partial class ModesSettingsTab : UserControl
{
    private ModesSettingsViewModel? _vm;
    private bool _updating;

    public ModesSettingsTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ModesSettingsViewModel vm)
        {
            _vm = vm;
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (_vm == null) return;

        // Localized labels
        AddModeBtn.Content = $"+ {Loc.L("添加模式", "Add Mode")}";
        EditTitle.Text = Loc.L("编辑模式", "Edit Mode");
        NameLabel.Text = Loc.L("名称", "Name");
        HotkeyLabel.Text = Loc.L("快捷键", "Hotkey");
        StyleLabel.Text = Loc.L("激活方式", "Activation Style");
        StyleToggle.Content = Loc.L("按一下开始/再按停止", "Toggle (press to start/stop)");
        StyleHold.Content = Loc.L("按住说话", "Hold to talk");
        PromptLabel.Text = Loc.L("提示词", "Prompt");
        PromptDesc.Text = Loc.L(
            "留空 = 直接输入模式。使用 {text} 占位符表示 ASR 识别结果。",
            "Empty = direct input mode. Use {text} as placeholder for ASR output.");

        // Bind list
        ModeListBox.ItemsSource = _vm.Modes;
        if (_vm.Modes.Count > 0)
            ModeListBox.SelectedIndex = 0;
    }

    private void ModeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || ModeListBox.SelectedItem is not ProcessingMode mode)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _updating = true;
        _vm.SelectedMode = mode;
        EditorPanel.Visibility = Visibility.Visible;

        ModeNameBox.Text = mode.Name;
        ModeNameBox.IsEnabled = !mode.IsBuiltin;
        PromptBox.Text = mode.Prompt;
        PromptBox.IsEnabled = !mode.IsBuiltin;

        HotkeyRecorder.SetHotkey(mode.HotkeyCode, mode.HotkeyModifiers);

        if (mode.HotkeyStyle == HotkeyStyle.Toggle)
            StyleToggle.IsChecked = true;
        else
            StyleHold.IsChecked = true;

        _updating = false;
    }

    private void AddMode_Click(object sender, RoutedEventArgs e)
    {
        _vm?.AddModeCommand.Execute(null);
        if (_vm?.SelectedMode != null)
            ModeListBox.SelectedItem = _vm.SelectedMode;
    }

    private void DeleteMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ProcessingMode mode)
        {
            _vm?.DeleteModeCommand.Execute(mode);
        }
    }

    private void ModeName_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updating || _vm?.SelectedMode == null) return;
        _vm.SelectedMode.Name = ModeNameBox.Text;
        _vm.Save();
        // Refresh listbox display
        ModeListBox.Items.Refresh();
    }

    private void Prompt_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updating || _vm?.SelectedMode == null) return;
        _vm.SelectedMode.Prompt = PromptBox.Text;
        _vm.Save();
    }

    private void HotkeyStyle_Changed(object sender, RoutedEventArgs e)
    {
        if (_updating || _vm?.SelectedMode == null) return;
        _vm.SelectedMode.HotkeyStyle = StyleToggle.IsChecked == true ? HotkeyStyle.Toggle : HotkeyStyle.Hold;
        _vm.Save();
    }

    private void HotkeyRecorder_Changed(object sender, EventArgs e)
    {
        if (_updating || _vm?.SelectedMode == null) return;
        _vm.SelectedMode.HotkeyCode = HotkeyRecorder.KeyCode;
        _vm.SelectedMode.HotkeyModifiers = HotkeyRecorder.Modifiers;
        _vm.Save();
    }

}
