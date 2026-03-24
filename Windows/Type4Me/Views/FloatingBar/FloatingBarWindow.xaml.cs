using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Type4Me.NativeMethods;
using Type4Me.Theme;
using Type4Me.ViewModels;

namespace Type4Me.Views.FloatingBar;

/// <summary>
/// Non-activating floating overlay window (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW).
/// </summary>
public partial class FloatingBarWindow : Window
{
    public FloatingBarWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make window non-activating
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.GetWindowLongW(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongW(hwnd, Win32.GWL_EXSTYLE,
            exStyle | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);

        // Position at bottom-center of primary screen
        PositionAtBottomCenter();
    }

    public void PositionAtBottomCenter()
    {
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2 + screen.Left;
        Top = screen.Bottom - Height - DesignTokens.BarBottomOffset;
    }

    public void ShowBar()
    {
        if (!IsVisible) Show();
        PositionAtBottomCenter();
    }

    public void HideBar()
    {
        if (IsVisible) Hide();
    }
}
