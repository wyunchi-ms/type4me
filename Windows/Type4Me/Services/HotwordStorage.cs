namespace Type4Me.Services;

/// <summary>
/// ASR hotword storage — persisted in settings.
/// </summary>
public static class HotwordStorage
{
    private const string Key = "hotwords";
    private static readonly string[] DefaultHotwords = ["claude", "claude code"];

    public static string[] Load()
    {
        var raw = SettingsService.Get(Key);
        if (raw == null)
        {
            // Seed defaults on first launch
            Save(DefaultHotwords);
            return DefaultHotwords;
        }

        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static void Save(string[] hotwords)
    {
        SettingsService.Set(Key, string.Join('\n', hotwords));
    }
}
