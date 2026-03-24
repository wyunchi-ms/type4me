using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Type4Me.Localization;
using Type4Me.ViewModels;

namespace Type4Me.Views.Settings;

/// <summary>
/// About tab: version info, data paths.
/// </summary>
public partial class AboutTab : UserControl
{
    private AboutViewModel? _vm;

    public AboutTab()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AboutViewModel vm)
        {
            _vm = vm;
            SetupUI();
        }
    }

    private void SetupUI()
    {
        if (_vm == null) return;

        TaglineText.Text = Loc.L("语音输入，随时随地", "Voice input, anywhere");

        VersionLabel.Text = Loc.L("版本", "Version");
        VersionValue.Text = _vm.Version;

        PlatformLabel.Text = Loc.L("平台", "Platform");
        PlatformValue.Text = _vm.Platform;

        EngineLabel.Text = Loc.L("引擎", "Engine");
        EngineValue.Text = _vm.Engine;

        DataPathLabel.Text = Loc.L("数据目录", "Data Path");
        DataPathValue.Text = _vm.DataPath;

        OpenFolderBtn.Content = Loc.L("打开数据目录", "Open Data Folder");
    }

    private void DataPath_Click(object sender, MouseButtonEventArgs e) => OpenDataFolder();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => OpenDataFolder();

    private void OpenDataFolder()
    {
        if (_vm == null) return;
        try
        {
            var dir = _vm.DataPath;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Ignore open failures
        }
    }
}
