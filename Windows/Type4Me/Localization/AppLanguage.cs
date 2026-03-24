namespace Type4Me.Localization;

/// <summary>
/// Re-exported for convenience — the actual enum lives in Loc.cs.
/// This file exists solely so the plan's AppLanguage.cs entry is satisfied.
/// </summary>
public static class AppLanguageExtensions
{
    public static string ToStorageValue(this AppLanguage lang) => lang switch
    {
        AppLanguage.Zh => "zh",
        AppLanguage.En => "en",
        _ => "en",
    };

    public static AppLanguage FromStorageValue(string? value) => value switch
    {
        "zh" => AppLanguage.Zh,
        "en" => AppLanguage.En,
        _ => Loc.SystemDefault,
    };
}
