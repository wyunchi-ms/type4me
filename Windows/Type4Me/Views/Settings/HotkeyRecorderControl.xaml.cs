using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Type4Me.Input;
using Type4Me.Localization;
using Type4Me.NativeMethods;

namespace Type4Me.Views.Settings;

/// <summary>
/// A control that records a hotkey combination when clicked.
/// Click → starts recording, next key press → captures hotkey, click again → cancels.
/// </summary>
public partial class HotkeyRecorderControl : UserControl
{
    private bool _isRecording;

    /// <summary>Captured virtual key code, or null if cleared.</summary>
    public int? KeyCode { get; private set; }

    /// <summary>Captured modifier flags (MOD_*).</summary>
    public uint? Modifiers { get; private set; }

    /// <summary>Fired when the hotkey changes.</summary>
    public event EventHandler? HotkeyChanged;

    public HotkeyRecorderControl()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            if (_isRecording)
                StopRecording();
        };
        UpdateDisplay();
    }

    /// <summary>Set the hotkey externally (when loading mode settings).</summary>
    public void SetHotkey(int? keyCode, uint? modifiers)
    {
        KeyCode = keyCode;
        Modifiers = modifiers;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (KeyCode.HasValue)
        {
            DisplayText.Text = KeyMapping.FormatHotkey(KeyCode, Modifiers);
            DisplayText.Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26));
            ClearBtn.Visibility = Visibility.Visible;
        }
        else
        {
            DisplayText.Text = Loc.L("点击设置快捷键", "Click to set hotkey");
            DisplayText.Foreground = new SolidColorBrush(Color.FromRgb(107, 107, 107));
            ClearBtn.Visibility = Visibility.Collapsed;
        }
    }

    private void Border_Click(object sender, MouseButtonEventArgs e)
    {
        if (ClearBtn.IsVisible && IsDescendantOf(e.OriginalSource as DependencyObject, ClearBtn))
            return;

        e.Handled = true;

        if (_isRecording)
        {
            StopRecording();
            return;
        }

        StartRecording();
    }

    private void StartRecording()
    {
        if (_isRecording) return;

        _isRecording = true;
        DisplayText.Text = Loc.L("请按下快捷键...", "Press a key...");
        DisplayText.Foreground = new SolidColorBrush(Color.FromRgb(199, 140, 38));
        RecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(199, 140, 38));
        RecorderBorder.Background = new SolidColorBrush(Color.FromRgb(255, 248, 230));
        ClearBtn.Visibility = Visibility.Collapsed;

        // Suppress the global low-level keyboard hook so recording-key presses
        // don't trigger a recording session in the background.
        var hk = App.ViewModel?.HotkeyManager;
        if (hk != null) hk.IsSuppressed = true;

        // Steal keyboard focus to this control so PreviewKeyDown fires here.
        Focusable = true;
        _ = Dispatcher.BeginInvoke(() => Keyboard.Focus(this), System.Windows.Threading.DispatcherPriority.Input);

        // Listen at the window level too, in case focus lands elsewhere
        // (e.g., a focused TextBox would otherwise eat the keystrokes).
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= OnPreviewKeyDown;
            window.PreviewKeyDown += OnPreviewKeyDown;
            window.Deactivated -= OwnerWindow_Deactivated;
            window.Deactivated += OwnerWindow_Deactivated;
            _ownerWindow = window;
        }
        PreviewKeyDown -= OnPreviewKeyDown;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private Window? _ownerWindow;

    private void StopRecording()
    {
        _isRecording = false;
        PreviewKeyDown -= OnPreviewKeyDown;
        if (_ownerWindow != null)
        {
            _ownerWindow.PreviewKeyDown -= OnPreviewKeyDown;
            _ownerWindow.Deactivated -= OwnerWindow_Deactivated;
            _ownerWindow = null;
        }

        var hk = App.ViewModel?.HotkeyManager;
        if (hk != null) hk.IsSuppressed = false;

        RecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(232, 227, 217));
        RecorderBorder.Background = new SolidColorBrush(Color.FromRgb(253, 250, 246));
        UpdateDisplay();
    }

    private void OwnerWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_isRecording)
            StopRecording();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording) return;

        e.Handled = true;

        // Resolve the actual key (System key when Alt is involved)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            StopRecording();
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0) return;

        // Detect which modifier keys are currently held (left/right specific)
        bool ctrlHeld = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        bool altHeld = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
        bool winHeld = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);

        bool keyIsModifier = IsModifierKey(vk);

        // Case A: a modifier key is the trigger itself (no other modifier held).
        // Capture it directly so users can bind Right Ctrl / Right Alt / Right Shift / etc.
        if (keyIsModifier)
        {
            int heldModifierCount = (ctrlHeld ? 1 : 0) + (shiftHeld ? 1 : 0) + (altHeld ? 1 : 0) + (winHeld ? 1 : 0);
            // The pressed modifier itself counts as one held modifier; require no OTHER modifier held.
            if (heldModifierCount > 1)
                return; // wait for a non-modifier key when combining modifiers

            KeyCode = vk;
            Modifiers = null;
            StopRecording();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Case B: a non-modifier key (optionally combined with modifiers).
        uint mods = 0;
        if (ctrlHeld) mods |= Win32.MOD_CONTROL;
        if (shiftHeld) mods |= Win32.MOD_SHIFT;
        if (altHeld) mods |= Win32.MOD_ALT;
        if (winHeld) mods |= Win32.MOD_WIN;

        KeyCode = vk;
        Modifiers = mods > 0 ? mods : null;

        StopRecording();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsModifierKey(int vk) =>
        vk == Win32.VK_CONTROL || vk == Win32.VK_SHIFT || vk == Win32.VK_MENU ||
        vk == Win32.VK_LCONTROL || vk == Win32.VK_RCONTROL ||
        vk == Win32.VK_LSHIFT || vk == Win32.VK_RSHIFT ||
        vk == Win32.VK_LMENU || vk == Win32.VK_RMENU ||
        vk == Win32.VK_LWIN || vk == Win32.VK_RWIN;

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
            StopRecording();

        KeyCode = null;
        Modifiers = null;
        UpdateDisplay();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsDescendantOf(DependencyObject? current, DependencyObject ancestor)
    {
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
    }

}
