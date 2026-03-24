using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Type4Me.Services;

/// <summary>
/// JSON-based settings store. Persists to %AppData%\Type4Me\settings.json.
/// Thread-safe via lock.
/// </summary>
public static class SettingsService
{
    private static readonly object _lock = new();

    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Type4Me");

    public static string SettingsFilePath => Path.Combine(_dir, "settings.json");

    static SettingsService()
    {
        Directory.CreateDirectory(_dir);
    }

    // ── Read / Write ───────────────────────────────────────

    public static string? Get(string key)
    {
        lock (_lock)
        {
            var root = LoadAll();
            return root.TryGetPropertyValue($"tf_{key}", out var node)
                ? node?.ToString()
                : null;
        }
    }

    public static T? Get<T>(string key, T? defaultValue = default)
    {
        var raw = Get(key);
        if (raw == null) return defaultValue;
        try
        {
            return JsonSerializer.Deserialize<T>(raw);
        }
        catch
        {
            return defaultValue;
        }
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        var raw = Get(key);
        if (raw == null) return defaultValue;
        return bool.TryParse(raw, out var v) ? v : defaultValue;
    }

    public static void Set(string key, string value)
    {
        lock (_lock)
        {
            var root = LoadAll();
            root[$"tf_{key}"] = value;
            SaveAll(root);
        }
    }

    public static void Set(string key, bool value) => Set(key, value.ToString().ToLowerInvariant());

    public static void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            var root = LoadAll();
            root[$"tf_{key}"] = JsonSerializer.SerializeToNode(value);
            SaveAll(root);
        }
    }

    public static void Remove(string key)
    {
        lock (_lock)
        {
            var root = LoadAll();
            root.Remove($"tf_{key}");
            SaveAll(root);
        }
    }

    // ── Internal ───────────────────────────────────────────

    private static JsonObject LoadAll()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return new JsonObject();
            var json = File.ReadAllText(SettingsFilePath);
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static void SaveAll(JsonObject root)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = root.ToJsonString(opts);
        File.WriteAllText(SettingsFilePath, json);
    }
}
