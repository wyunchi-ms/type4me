using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Type4Me.Models;
using Type4Me.Theme;
using Type4Me.ViewModels;

namespace Type4Me.Views.FloatingBar;

/// <summary>
/// Floating bar view — shows capsule with phase-dependent content.
/// </summary>
public partial class FloatingBarView : UserControl
{
    public FloatingBarView()
    {
        InitializeComponent();
    }

    /// <summary>Update visual state based on current phase.</summary>
    public void UpdatePhase(FloatingBarPhase phase, FloatingBarViewModel vm)
    {
        switch (phase)
        {
            case FloatingBarPhase.Hidden:
                Visibility = Visibility.Collapsed;
                break;

            case FloatingBarPhase.Preparing:
                Visibility = Visibility.Visible;
                StatusDot.Fill = new SolidColorBrush(DesignTokens.Amber);
                ContentText.Text = Localization.Loc.L("准备中...", "Preparing...");
                break;

            case FloatingBarPhase.Recording:
                Visibility = Visibility.Visible;
                StatusDot.Fill = new SolidColorBrush(DesignTokens.Recording);
                ContentText.Text = vm.TranscriptionText;
                break;

            case FloatingBarPhase.Processing:
                Visibility = Visibility.Visible;
                StatusDot.Fill = new SolidColorBrush(DesignTokens.Amber);
                ContentText.Text = vm.ProcessingLabel;
                break;

            case FloatingBarPhase.Done:
                Visibility = Visibility.Visible;
                StatusDot.Fill = new SolidColorBrush(DesignTokens.Success);
                ContentText.Text = vm.FeedbackMessage;
                break;

            case FloatingBarPhase.Error:
                Visibility = Visibility.Visible;
                StatusDot.Fill = new SolidColorBrush(Colors.Red);
                ContentText.Text = vm.FeedbackMessage;
                break;
        }
    }
}
