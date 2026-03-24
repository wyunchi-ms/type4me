using System.Runtime.InteropServices;
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
        if (_isRecording)
        {
            StopRecording();
            return;
        }

        StartRecording();
    }

    private void StartRecording()
    {
        _isRecording = true;
        DisplayText.Text = Loc.L("请按下快捷键...", "Press a key...");
        DisplayText.Foreground = new SolidColorBrush(Color.FromRgb(199, 140, 38));
        RecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(199, 140, 38));
        ClearBtn.Visibility = Visibility.Collapsed;

        Focus();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void StopRecording()
    {
        _isRecording = false;
        PreviewKeyDown -= OnPreviewKeyDown;
        RecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(232, 227, 217));
        UpdateDisplay();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Get the actual VK code
        int vk = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

        // Skip pure modifier keys — wait for a non-modifier
        if (IsModifierKey(vk))
            return;

        // Capture modifiers
        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            mods |= Win32.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            mods |= Win32.MOD_SHIFT;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            mods |= Win32.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            mods |= Win32.MOD_WIN;

        // Distinguish left/right for Ctrl, Shift, Alt
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt)
        {
            // User pressed just a modifier key as the hotkey itself (e.g., Right Ctrl alone)
            vk = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);
            mods = 0; // The modifier IS the key, not a modifier
        }

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
        KeyCode = null;
        Modifiers = null;
        UpdateDisplay();
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_isRecording)
            StopRecording();
    }
}
