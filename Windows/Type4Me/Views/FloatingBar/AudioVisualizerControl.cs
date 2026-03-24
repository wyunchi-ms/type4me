using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Type4Me.Views.FloatingBar;

/// <summary>
/// Canvas-based audio waveform visualizer for the floating bar.
/// Renders a simple level-based bar visualization.
/// </summary>
public class AudioVisualizerControl : Canvas
{
    public static readonly DependencyProperty AudioLevelProperty =
        DependencyProperty.Register(
            nameof(AudioLevel), typeof(double), typeof(AudioVisualizerControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double AudioLevel
    {
        get => (double)GetValue(AudioLevelProperty);
        set => SetValue(AudioLevelProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var level = Math.Clamp(AudioLevel, 0, 1);
        var barCount = 12;
        var barWidth = w / (barCount * 2.0);
        var gap = barWidth;

        var brush = new SolidColorBrush(Theme.DesignTokens.Recording);
        brush.Opacity = 0.6;

        var startX = (w - barCount * (barWidth + gap) + gap) / 2;

        for (int i = 0; i < barCount; i++)
        {
            // Create varied height based on level and position
            var t = (double)i / (barCount - 1);
            var envelope = Math.Sin(Math.PI * t); // Bell curve
            var barHeight = Math.Max(2, h * level * envelope * 0.8);

            var x = startX + i * (barWidth + gap);
            var y = (h - barHeight) / 2;

            dc.DrawRoundedRectangle(brush, null,
                new Rect(x, y, barWidth, barHeight),
                barWidth / 2, barWidth / 2);
        }
    }
}
