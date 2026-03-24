using System.Windows.Media;

namespace Type4Me.Theme;

/// <summary>
/// Design tokens — colors, spacing, corner radii, dimensions.
/// Mirrors the macOS TF namespace.
/// </summary>
public static class DesignTokens
{
    // ── Colors ──────────────────────────────────────────────

    /// <summary>Warm amber accent: the signature "indicator light" color.</summary>
    public static readonly Color Amber = Color.FromRgb(212, 145, 61);          // ~(0.83, 0.57, 0.24)

    /// <summary>Recording active: warm red-orange, urgent but not alarming.</summary>
    public static readonly Color Recording = Color.FromRgb(222, 97, 77);       // ~(0.87, 0.38, 0.30)

    /// <summary>Success: muted warm green.</summary>
    public static readonly Color Success = Color.FromRgb(107, 179, 107);       // ~(0.42, 0.70, 0.42)

    // ── Settings Palette ────────────────────────────────────

    public static readonly Color SettingsBg          = Color.FromRgb(242, 235, 224);  // (0.95, 0.92, 0.88)
    public static readonly Color SettingsCard        = Color.FromRgb(250, 245, 237);  // (0.98, 0.96, 0.93)
    public static readonly Color SettingsCardAlt     = Color.FromRgb(232, 227, 217);  // (0.91, 0.89, 0.85)
    public static readonly Color SettingsNavActive   = Color.FromRgb(26, 26, 26);     // (0.10, 0.10, 0.10)
    public static readonly Color SettingsText        = Color.FromRgb(26, 26, 26);
    public static readonly Color SettingsTextSecondary = Color.FromRgb(61, 61, 61);   // (0.24, 0.24, 0.24)
    public static readonly Color SettingsTextTertiary  = Color.FromRgb(107, 107, 107);// (0.42, 0.42, 0.42)
    public static readonly Color SettingsAccentGreen = Color.FromRgb(77, 158, 89);    // (0.30, 0.62, 0.35)
    public static readonly Color SettingsAccentAmber = Color.FromRgb(199, 140, 38);   // (0.78, 0.55, 0.15)
    public static readonly Color SettingsAccentRed   = Color.FromRgb(204, 71, 56);    // (0.80, 0.28, 0.22)

    // ── Brushes (pre-allocated for XAML binding) ────────────

    public static readonly SolidColorBrush AmberBrush          = Freeze(new SolidColorBrush(Amber));
    public static readonly SolidColorBrush RecordingBrush      = Freeze(new SolidColorBrush(Recording));
    public static readonly SolidColorBrush SuccessBrush        = Freeze(new SolidColorBrush(Success));
    public static readonly SolidColorBrush SettingsBgBrush     = Freeze(new SolidColorBrush(SettingsBg));
    public static readonly SolidColorBrush SettingsCardBrush   = Freeze(new SolidColorBrush(SettingsCard));
    public static readonly SolidColorBrush SettingsTextBrush   = Freeze(new SolidColorBrush(SettingsText));
    public static readonly SolidColorBrush SettingsTextSecBrush = Freeze(new SolidColorBrush(SettingsTextSecondary));
    public static readonly SolidColorBrush SettingsAccentGreenBrush = Freeze(new SolidColorBrush(SettingsAccentGreen));
    public static readonly SolidColorBrush SettingsAccentAmberBrush = Freeze(new SolidColorBrush(SettingsAccentAmber));

    // ── Spacing ─────────────────────────────────────────────

    public const double SpacingXS = 4;
    public const double SpacingSM = 8;
    public const double SpacingMD = 12;
    public const double SpacingLG = 16;
    public const double SpacingXL = 24;

    // ── Corner Radius ───────────────────────────────────────

    public const double CornerSM = 6;
    public const double CornerMD = 10;
    public const double CornerLG = 16;

    // ── Floating Bar ────────────────────────────────────────

    public const double BarWidth = 400;
    public const double BarWidthCompact = 200;
    public const double BarHeight = 52;
    public const double BarBottomOffset = 48;

    // ── Animation Durations (ms) ────────────────────────────

    public const int AnimSnappy = 250;
    public const int AnimGentle = 400;
    public const int AnimBouncy = 350;
    public const int AnimQuick  = 200;

    // ── Helpers ─────────────────────────────────────────────

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
