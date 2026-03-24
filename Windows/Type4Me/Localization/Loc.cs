using System.Globalization;
using System.IO;

namespace Type4Me.Localization;

/// <summary>
/// Supported application languages.
/// </summary>
public enum AppLanguage
{
    Zh,
    En,
}

/// <summary>
/// Inline localization helper — mirrors the macOS L("中文", "English") pattern.
/// </summary>
public static class Loc
{
    private const string LanguageSettingKey = "language";

    private static AppLanguage? _override;

    /// <summary>
    /// Current application language. Reads from settings, falls back to system default.
    /// </summary>
    public static AppLanguage Current
    {
        get
        {
            if (_override.HasValue) return _override.Value;

            var saved = SettingsHelper.Read(LanguageSettingKey);
            if (saved != null && Enum.TryParse<AppLanguage>(saved, true, out var lang))
                return lang;

            return SystemDefault;
        }
        set => _override = value;
    }

    /// <summary>
    /// System default: Chinese if system culture starts with "zh", otherwise English.
    /// </summary>
    public static AppLanguage SystemDefault
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Zh
                : AppLanguage.En;
        }
    }

    /// <summary>
    /// Returns the Chinese or English string based on current language setting.
    /// Usage: Loc.L("中文", "English")
    /// </summary>
    public static string L(string zh, string en) => Current == AppLanguage.Zh ? zh : en;

    /// <summary>
    /// Display name for a language.
    /// </summary>
    public static string DisplayName(AppLanguage lang) => lang switch
    {
        AppLanguage.Zh => "中文",
        AppLanguage.En => "English",
        _ => "English",
    };

    /// <summary>
    /// Helper to read a single setting from the settings store (avoids circular dependency).
    /// </summary>
    private static class SettingsHelper
    {
        public static string? Read(string key)
        {
            try
            {
                var path = Services.SettingsService.SettingsFilePath;
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty($"tf_{key}", out var val))
                    return val.GetString();
            }
            catch { /* ignore */ }
            return null;
        }
    }
}
