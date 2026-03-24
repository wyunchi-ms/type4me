using System.Windows;
using System.Windows.Controls;
using Type4Me.ASR;
using Type4Me.LLM;
using Type4Me.Localization;
using Type4Me.Services;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// General settings tab: ASR/LLM provider selection, credentials, preferences.
/// </summary>
public partial class GeneralSettingsTab : UserControl
{
    private GeneralSettingsViewModel? _vm;
    private bool _initializing = true;

    public GeneralSettingsTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is GeneralSettingsViewModel vm)
        {
            _vm = vm;
            _initializing = true;
            SetupUI();
            _initializing = false;
        }
    }

    private void SetupUI()
    {
        if (_vm == null) return;

        // Localized labels
        ASRTitle.Text = Loc.L("语音识别 (ASR)", "Speech Recognition (ASR)");
        ASRProviderLabel.Text = Loc.L("提供商", "Provider");
        LLMTitle.Text = Loc.L("语言模型 (LLM)", "Language Model (LLM)");
        LLMProviderLabel.Text = Loc.L("提供商", "Provider");
        PrefsTitle.Text = Loc.L("偏好设置", "Preferences");
        EnableSoundCheck.Content = Loc.L("启用提示音", "Enable sound effects");
        LaunchAtLoginCheck.Content = Loc.L("开机自启动", "Launch at login");
        LanguageLabel.Text = Loc.L("语言", "Language");
        SaveASRBtn.Content = Loc.L("保存", "Save");
        SaveLLMBtn.Content = Loc.L("保存", "Save");

        // ASR provider combo
        ASRProviderCombo.Items.Clear();
        foreach (ASRProvider p in Enum.GetValues<ASRProvider>())
            ASRProviderCombo.Items.Add(new ComboBoxItem { Content = p.DisplayName(), Tag = p });
        ASRProviderCombo.SelectedIndex = (int)_vm.SelectedASRProvider;

        // LLM provider combo
        LLMProviderCombo.Items.Clear();
        foreach (LLMProvider p in Enum.GetValues<LLMProvider>())
            LLMProviderCombo.Items.Add(new ComboBoxItem { Content = p.DisplayName(), Tag = p });
        LLMProviderCombo.SelectedIndex = (int)_vm.SelectedLLMProvider;

        // Preferences
        EnableSoundCheck.IsChecked = _vm.EnableSound;
        LaunchAtLoginCheck.IsChecked = _vm.LaunchAtLogin;

        // Language combo
        LanguageCombo.Items.Clear();
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "中文", Tag = AppLanguage.Zh });
        LanguageCombo.Items.Add(new ComboBoxItem { Content = "English", Tag = AppLanguage.En });
        LanguageCombo.SelectedIndex = _vm.Language == AppLanguage.Zh ? 0 : 1;

        // Render credential fields
        RenderASRFields();
        RenderLLMFields();
    }

    // ── ASR Credential Fields ────────────────────────────

    private void RenderASRFields()
    {
        if (_vm == null) return;
        ASRFieldsPanel.Items.Clear();

        foreach (var field in _vm.ASRFields)
        {
            var panel = CreateFieldPanel(field, _vm.AsrCredentials);
            ASRFieldsPanel.Items.Add(panel);
        }
    }

    private void RenderLLMFields()
    {
        if (_vm == null) return;
        LLMFieldsPanel.Items.Clear();

        foreach (var field in _vm.LLMFields)
        {
            var panel = CreateFieldPanel(field, _vm.LlmCredentials);
            LLMFieldsPanel.Items.Add(panel);
        }
    }

    private StackPanel CreateFieldPanel(CredentialField field, Dictionary<string, string> credentials)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        var label = new TextBlock
        {
            Text = field.IsOptional ? $"{field.Label} ({Loc.L("可选", "optional")})" : field.Label,
            FontSize = 12,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(61, 61, 61)),
            Margin = new Thickness(0, 0, 0, 4),
        };
        panel.Children.Add(label);

        credentials.TryGetValue(field.Key, out var currentValue);

        if (field.IsSecure)
        {
            var pwBox = new PasswordBox
            {
                Password = currentValue ?? field.DefaultValue,
                Tag = field.Key,
                MinWidth = 300,
                MaxWidth = 400,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            pwBox.PasswordChanged += (s, _) =>
            {
                if (s is PasswordBox pb)
                    credentials[field.Key] = pb.Password;
            };
            panel.Children.Add(pwBox);
        }
        else
        {
            var textBox = new TextBox
            {
                Text = currentValue ?? field.DefaultValue,
                Tag = field.Key,
                MinWidth = 300,
                MaxWidth = 400,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            if (!string.IsNullOrEmpty(field.Placeholder))
            {
                // Simple placeholder via GotFocus/LostFocus
                textBox.GotFocus += (s, _) =>
                {
                    if (s is TextBox tb && tb.Text == field.Placeholder)
                    {
                        tb.Text = "";
                        tb.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(26, 26, 26));
                    }
                };
            }
            textBox.TextChanged += (s, _) =>
            {
                if (s is TextBox tb)
                    credentials[field.Key] = tb.Text;
            };
            panel.Children.Add(textBox);
        }

        return panel;
    }

    // ── Event Handlers ───────────────────────────────────

    private void ASRProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _vm == null || ASRProviderCombo.SelectedItem == null) return;
        if (ASRProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is ASRProvider provider)
        {
            _vm.SelectedASRProvider = provider;
            RenderASRFields();
        }
    }

    private void LLMProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _vm == null || LLMProviderCombo.SelectedItem == null) return;
        if (LLMProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is LLMProvider provider)
        {
            _vm.SelectedLLMProvider = provider;
            RenderLLMFields();
        }
    }

    private void SaveASR_Click(object sender, RoutedEventArgs e)
    {
        _vm?.SaveASRCredentialsCommand.Execute(null);
        StatusText.Text = _vm?.StatusMessage ?? "";
    }

    private void SaveLLM_Click(object sender, RoutedEventArgs e)
    {
        _vm?.SaveLLMCredentialsCommand.Execute(null);
        StatusText.Text = _vm?.StatusMessage ?? "";
    }

    private void EnableSound_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _vm == null) return;
        _vm.EnableSound = EnableSoundCheck.IsChecked == true;
        SettingsService.Set("enableSound", _vm.EnableSound);
    }

    private void LaunchAtLogin_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _vm == null) return;
        _vm.LaunchAtLogin = LaunchAtLoginCheck.IsChecked == true;
        SettingsService.Set("launchAtLogin", _vm.LaunchAtLogin);
        UpdateAutoStart(_vm.LaunchAtLogin);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _vm == null || LanguageCombo.SelectedItem == null) return;
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is AppLanguage lang)
            _vm.Language = lang;
    }

    private static void UpdateAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                    key.SetValue("Type4Me", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("Type4Me", false);
            }
        }
        catch
        {
            // Registry access may fail in restricted environments
        }
    }
}
