using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Type4Me.NativeMethods;
using Type4Me.Theme;
using Type4Me.ViewModels;

namespace Type4Me.Views.FloatingBar;

/// <summary>
/// Non-activating floating overlay window (WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW).
/// Uses Opacity rather than Show/Hide to avoid WPF window flicker on transparent windows.
/// </summary>
public partial class FloatingBarWindow : Window
{
    private bool _isBarVisible;

    public FloatingBarWindow()
    {
        InitializeComponent();
        Opacity = 0;
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FloatingBarViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is FloatingBarViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not FloatingBarViewModel vm) return;

        // Update view content on any relevant property change
        if (e.PropertyName is nameof(FloatingBarViewModel.Phase)
            or nameof(FloatingBarViewModel.TranscriptionText)
            or nameof(FloatingBarViewModel.FeedbackMessage)
            or nameof(FloatingBarViewModel.ProcessingLabel))
        {
            Dispatcher.Invoke(() => BarView.UpdatePhase(vm.Phase, vm));
        }
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

    /// <summary>Make the bar visible (no-op if already visible).</summary>
    public void ShowBar()
    {
        if (_isBarVisible) return;
        _isBarVisible = true;
        Opacity = 1;
        IsHitTestVisible = true;
        PositionAtBottomCenter();
    }

    /// <summary>Make the bar invisible (no-op if already hidden).</summary>
    public void HideBar()
    {
        if (!_isBarVisible) return;
        _isBarVisible = false;
        Opacity = 0;
        IsHitTestVisible = false;
    }
}
