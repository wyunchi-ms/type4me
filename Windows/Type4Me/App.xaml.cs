using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Type4Me.Localization;
using Type4Me.Services;
using Type4Me.ViewModels;
using Type4Me.Views.FloatingBar;
using Type4Me.Views.Setup;

namespace Type4Me;

/// <summary>
/// Application entry point — system tray icon, single-instance enforcement,
/// AppViewModel wiring, floating bar, and setup wizard.
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private Mutex? _singleInstanceMutex;
    private AppViewModel? _appViewModel;
    private FloatingBarWindow? _floatingBarWindow;
    private Views.Settings.SettingsWindow? _settingsWindow;
    private Views.Debug.DebugLogWindow? _debugWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Global Exception Handlers ────────────────────────
        DispatcherUnhandledException += (_, args) =>
        {
            DebugFileLogger.Log($"[CRASH] UI thread: {args.Exception}");
            MessageBox.Show(
                $"Unexpected error:\n{args.Exception.Message}\n\nSee log for details.",
                "Type4Me Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                DebugFileLogger.Log($"[CRASH] AppDomain: {ex}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DebugFileLogger.Log($"[CRASH] UnobservedTask: {args.Exception}");
            args.SetObserved();
        };

        // ── Single Instance ────────────────────────────────
        _singleInstanceMutex = new Mutex(true, "Type4Me_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show(
                Loc.L("Type4Me 已在运行。", "Type4Me is already running."),
                "Type4Me",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ── Initialization ─────────────────────────────────
        CredentialService.MigrateIfNeeded();
        DebugFileLogger.StartSession();
        DebugFileLogger.Log("App startup");

        // ── System Tray Icon ───────────────────────────────
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Type4Me",
            ContextMenu = CreateTrayMenu(),
        };

        // Try to load icon from resources
        try
        {
            var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"))?.Stream;
            if (iconStream != null)
                _trayIcon.Icon = new Icon(iconStream);
        }
        catch
        {
            // Use default icon if resource not found
        }

        // ── AppViewModel + Floating Bar ─────────────────────
        try
        {
            _appViewModel = new AppViewModel();
            _floatingBarWindow = new FloatingBarWindow();
            _floatingBarWindow.DataContext = _appViewModel.FloatingBar;

            // Show/hide floating bar based on phase changes
            _appViewModel.FloatingBar.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FloatingBarViewModel.Phase))
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_appViewModel.FloatingBar.Phase == Models.FloatingBarPhase.Hidden)
                            _floatingBarWindow.Hide();
                        else if (!_floatingBarWindow.IsVisible)
                            _floatingBarWindow.Show();
                    });
                }
            };

            _appViewModel.Start();
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[Startup] AppViewModel/FloatingBar error: {ex}");
            // Continue anyway — settings and tray still work
        }

        // ── Setup Wizard (first launch) ─────────────────────
        if (!SetupWizardViewModel.HasCompletedSetup)
        {
            // Show wizard after a brief delay to let the tray icon settle
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                ShowSetupWizard();
            };
            timer.Start();
        }
    }

    private System.Windows.Controls.ContextMenu CreateTrayMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var preferencesItem = new System.Windows.Controls.MenuItem
        {
            Header = Loc.L("偏好设置", "Preferences"),
        };
        preferencesItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(preferencesItem);

        var wizardItem = new System.Windows.Controls.MenuItem
        {
            Header = Loc.L("设置向导...", "Setup Wizard..."),
        };
        wizardItem.Click += (_, _) => ShowSetupWizard();
        menu.Items.Add(wizardItem);

        var debugItem = new System.Windows.Controls.MenuItem
        {
            Header = Loc.L("调试控制台", "Debug Console"),
        };
        debugItem.Click += (_, _) => ShowDebugConsole();
        menu.Items.Add(debugItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var quitItem = new System.Windows.Controls.MenuItem
        {
            Header = Loc.L("退出", "Quit"),
        };
        quitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(quitItem);

        return menu;
    }

    private void ShowSettings()
    {
        DebugFileLogger.Log("Settings requested");

        try
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new Views.Settings.SettingsWindow();
            _settingsWindow.Closed += (_, _) =>
            {
                _settingsWindow = null;
                _appViewModel?.ReloadModes();
            };
            _settingsWindow.Show();
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[ShowSettings] Error: {ex}");
            MessageBox.Show($"Failed to open settings:\n{ex.Message}", "Type4Me Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private SetupWizardWindow? _setupWizard;

    private void ShowDebugConsole()
    {
        if (_appViewModel == null) return;

        if (_debugWindow != null && _debugWindow.IsLoaded)
        {
            _debugWindow.Activate();
            return;
        }

        _debugWindow = new Views.Debug.DebugLogWindow();
        _debugWindow.DataContext = _appViewModel.DebugLog;
        _debugWindow.Closed += (_, _) => _debugWindow = null;
        _debugWindow.Show();
    }

    private void ShowSetupWizard()
    {
        DebugFileLogger.Log("Setup wizard requested");

        if (_setupWizard != null && _setupWizard.IsLoaded)
        {
            _setupWizard.Activate();
            return;
        }

        _setupWizard = new SetupWizardWindow();
        _setupWizard.Closed += (_, _) =>
        {
            _setupWizard = null;
            // Reload modes/credentials after wizard
            _appViewModel?.ReloadModes();
        };
        _setupWizard.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugFileLogger.Log("App exit");
        _appViewModel?.Stop();
        _floatingBarWindow?.Close();
        _debugWindow?.Close();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
