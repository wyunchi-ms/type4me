using System.IO;

namespace Type4Me.Services;

/// <summary>
/// Stores ASR customization settings: boosting table ID, context history length, stable UID.
/// </summary>
public static class ASRCustomizationStorage
{
    private const string UidKey = "asrUID";
    private const string BiasSettingsKey = "asrBiasSettings";

    // ── UID (stable per-install) ───────────────────────────

    public static string LoadOrCreateUID()
    {
        var existing = SettingsService.Get(UidKey);
        if (!string.IsNullOrEmpty(existing))
            return existing;

        var uid = $"type4me-{Guid.NewGuid():N}";
        SettingsService.Set(UidKey, uid);
        return uid;
    }

    // ── Bias Settings ──────────────────────────────────────

    public static string? GetBoostingTableID() =>
        SettingsService.Get("asrBoostingTableID");

    public static void SetBoostingTableID(string? value)
    {
        if (string.IsNullOrEmpty(value))
            SettingsService.Remove("asrBoostingTableID");
        else
            SettingsService.Set("asrBoostingTableID", value);
    }

    public static int GetContextHistoryLength() =>
        int.TryParse(SettingsService.Get("asrContextHistoryLength"), out var v) ? v : 20;

    public static void SetContextHistoryLength(int value) =>
        SettingsService.Set("asrContextHistoryLength", value.ToString());
}
