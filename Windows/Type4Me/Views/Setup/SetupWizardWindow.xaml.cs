using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.ViewModels;

namespace Type4Me.Views.Setup;

/// <summary>
/// Setup wizard window — guides first-time users through configuration.
/// Steps: Welcome → Quick Mode Demo → Custom Mode Demo → Provider/Credentials → Ready.
/// </summary>
public partial class SetupWizardWindow : Window
{
    private readonly SetupWizardViewModel _vm;

    public SetupWizardWindow()
    {
        _vm = new SetupWizardViewModel();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SetupWizardViewModel.CurrentStep))
                ShowStep(_vm.CurrentStep);
        };
        _vm.OnCompleted += () => Close();

        InitializeComponent();
        ShowStep(SetupWizardViewModel.WizardStep.Welcome);
    }

    private void ShowStep(SetupWizardViewModel.WizardStep step)
    {
        ContentGrid.Children.Clear();
        UIElement content = step switch
        {
            SetupWizardViewModel.WizardStep.Welcome => BuildWelcomeStep(),
            SetupWizardViewModel.WizardStep.QuickDemo => BuildQuickDemoStep(),
            SetupWizardViewModel.WizardStep.CustomDemo => BuildCustomDemoStep(),
            SetupWizardViewModel.WizardStep.Provider => BuildProviderStep(),
            SetupWizardViewModel.WizardStep.Ready => BuildReadyStep(),
            _ => new TextBlock { Text = "Unknown step" },
        };
        ContentGrid.Children.Add(content);
    }

    // ── Step 0: Welcome ──────────────────────────────────

    private UIElement BuildWelcomeStep()
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        // App icon
        var iconBorder = new Border
        {
            Width = 80, Height = 80, CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromRgb(212, 145, 61)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        iconBorder.Child = new TextBlock
        {
            Text = "T4M", FontSize = 24, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(iconBorder);

        panel.Children.Add(new TextBlock
        {
            Text = "Type4Me",
            FontSize = 28, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });

        panel.Children.Add(new TextBlock
        {
            Text = Loc.L("说出来，它来打", "Speak, and it types"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 107, 107)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 32),
        });

        var startBtn = CreatePrimaryButton(Loc.L("开始设置", "Get Started"));
        startBtn.Click += (_, _) => _vm.NextStepCommand.Execute(null);
        panel.Children.Add(startBtn);

        return panel;
    }

    // ── Step 1: Quick Mode Demo ──────────────────────────

    private UIElement BuildQuickDemoStep()
    {
        var dockPanel = new DockPanel();

        // Footer
        var footer = BuildNavigationFooter(
            showBack: true,
            nextLabel: Loc.L("下一步", "Next"));
        DockPanel.SetDock(footer, Dock.Bottom);
        dockPanel.Children.Add(footer);

        // Content
        var content = new QuickModeDemoStep { Margin = new Thickness(24) };
        dockPanel.Children.Add(content);

        return dockPanel;
    }

    // ── Step 2: Custom Mode Demo ─────────────────────────

    private UIElement BuildCustomDemoStep()
    {
        var dockPanel = new DockPanel();

        var footer = BuildNavigationFooter(
            showBack: true,
            nextLabel: Loc.L("下一步", "Next"));
        DockPanel.SetDock(footer, Dock.Bottom);
        dockPanel.Children.Add(footer);

        var content = new CustomModeDemoStep { Margin = new Thickness(24) };
        dockPanel.Children.Add(content);

        return dockPanel;
    }

    // ── Step 3: Provider/Credentials ─────────────────────

    private UIElement BuildProviderStep()
    {
        var dockPanel = new DockPanel();

        var footer = BuildNavigationFooter(
            showBack: true,
            nextLabel: Loc.L("保存并继续", "Save & Continue"),
            isNext: false);
        DockPanel.SetDock(footer, Dock.Bottom);
        dockPanel.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(24),
        };

        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = Loc.L("配置语音识别", "Configure Speech Recognition"),
            FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            Margin = new Thickness(0, 0, 0, 8),
        });

        panel.Children.Add(new TextBlock
        {
            Text = Loc.L(
                "选择 ASR 提供商并填入凭证。您可以稍后在设置中修改。",
                "Choose an ASR provider and enter credentials. You can change this later in Settings."),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 107, 107)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        // Provider combo
        panel.Children.Add(new TextBlock
        {
            Text = Loc.L("提供商", "Provider"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(61, 61, 61)),
            Margin = new Thickness(0, 0, 0, 4),
        });

        var combo = new ComboBox { Margin = new Thickness(0, 0, 0, 16), MaxWidth = 400, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (ASRProvider p in Enum.GetValues<ASRProvider>())
            combo.Items.Add(new ComboBoxItem { Content = p.DisplayName(), Tag = p });
        combo.SelectedIndex = (int)_vm.SelectedASRProvider;
        combo.SelectionChanged += (s, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is ASRProvider provider)
            {
                _vm.SelectedASRProvider = provider;
                // Rebuild to refresh credential fields
                ShowStep(SetupWizardViewModel.WizardStep.Provider);
            }
        };
        panel.Children.Add(combo);

        // Credential fields
        var fieldsPanel = new StackPanel();
        foreach (var field in _vm.ASRFields)
        {
            fieldsPanel.Children.Add(new TextBlock
            {
                Text = field.IsOptional ? $"{field.Label} ({Loc.L("可选", "optional")})" : field.Label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(61, 61, 61)),
                Margin = new Thickness(0, 0, 0, 4),
            });

            _vm.AsrCredentials.TryGetValue(field.Key, out var currentValue);

            if (field.IsSecure)
            {
                var pwBox = new PasswordBox
                {
                    Password = currentValue ?? field.DefaultValue,
                    MaxWidth = 400,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 12),
                };
                pwBox.PasswordChanged += (s, _) =>
                {
                    if (s is PasswordBox pb)
                        _vm.AsrCredentials[field.Key] = pb.Password;
                };
                fieldsPanel.Children.Add(pwBox);
            }
            else
            {
                var textBox = new TextBox
                {
                    Text = currentValue ?? field.DefaultValue,
                    MaxWidth = 400,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 12),
                };
                textBox.TextChanged += (s, _) =>
                {
                    if (s is TextBox tb)
                        _vm.AsrCredentials[field.Key] = tb.Text;
                };
                fieldsPanel.Children.Add(textBox);
            }
        }
        panel.Children.Add(fieldsPanel);

        scroll.Content = panel;
        dockPanel.Children.Add(scroll);

        return dockPanel;
    }

    // ── Step 4: Ready ────────────────────────────────────

    private UIElement BuildReadyStep()
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        // Checkmark
        var checkBorder = new Border
        {
            Width = 64, Height = 64, CornerRadius = new CornerRadius(32),
            Background = new SolidColorBrush(Color.FromRgb(77, 158, 89)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        checkBorder.Child = new TextBlock
        {
            Text = "✓", FontSize = 28, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(checkBorder);

        panel.Children.Add(new TextBlock
        {
            Text = Loc.L("准备就绪！", "You're all set!"),
            FontSize = 24, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        });

        panel.Children.Add(new TextBlock
        {
            Text = Loc.L(
                "按下右 Ctrl 键开始说话，再按一次停止。",
                "Press Right Ctrl to start speaking, press again to stop."),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 107, 107)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 400,
            Margin = new Thickness(0, 0, 0, 32),
        });

        var finishBtn = CreatePrimaryButton(Loc.L("开始使用", "Start Using"));
        finishBtn.Click += (_, _) => _vm.FinishCommand.Execute(null);
        panel.Children.Add(finishBtn);

        return panel;
    }

    // ── Shared UI Components ─────────────────────────────

    private StackPanel BuildNavigationFooter(bool showBack, string nextLabel, bool isNext = true)
    {
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 24),
        };

        if (showBack)
        {
            var backBtn = CreateSecondaryButton(Loc.L("上一步", "Back"));
            backBtn.Click += (_, _) => _vm.PreviousStepCommand.Execute(null);
            backBtn.Margin = new Thickness(0, 0, 12, 0);
            footer.Children.Add(backBtn);
        }

        var nextBtn = CreatePrimaryButton(nextLabel);
        if (isNext)
            nextBtn.Click += (_, _) => _vm.NextStepCommand.Execute(null);
        else
            nextBtn.Click += (_, _) => _vm.SaveAndContinueCommand.Execute(null);
        footer.Children.Add(nextBtn);

        return footer;
    }

    private static Button CreatePrimaryButton(string text)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 14,
            Padding = new Thickness(24, 10, 24, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.Template = CreateRoundedButtonTemplate(
            Color.FromRgb(26, 26, 26), Colors.White, Color.FromRgb(51, 51, 51));
        return btn;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 14,
            Padding = new Thickness(24, 10, 24, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.Template = CreateRoundedButtonTemplate(
            Color.FromRgb(232, 227, 217), Color.FromRgb(26, 26, 26), Color.FromRgb(220, 215, 205));
        return btn;
    }

    private static ControlTemplate CreateRoundedButtonTemplate(Color bg, Color fg, Color hover)
    {
        var template = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bg));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(24, 10, 24, 10));
        borderFactory.Name = "Bd";

        var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        cpFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cpFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(fg));

        borderFactory.AppendChild(cpFactory);
        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "Bd"));
        template.Triggers.Add(hoverTrigger);

        return template;
    }
}
