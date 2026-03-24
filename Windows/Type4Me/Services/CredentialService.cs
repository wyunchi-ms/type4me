using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Type4Me.Services;

/// <summary>
/// Credential storage — persists to %AppData%\Type4Me\credentials.json.
/// Mirrors the macOS KeychainService (file-based, not actual Keychain).
/// Thread-safe via lock.
/// </summary>
public static class CredentialService
{
    private static readonly object _lock = new();

    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Type4Me");

    public static string CredentialsFilePath => Path.Combine(_dir, "credentials.json");

    static CredentialService()
    {
        Directory.CreateDirectory(_dir);
    }

    // ── Core Read/Write ────────────────────────────────────

    private static JsonObject LoadAll()
    {
        try
        {
            if (!File.Exists(CredentialsFilePath)) return new JsonObject();
            var json = File.ReadAllText(CredentialsFilePath);
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static void SaveAll(JsonObject root)
    {
        var opts = new JsonSerializerOptions { WriteIndented = false };
        var json = root.ToJsonString(opts);
        File.WriteAllText(CredentialsFilePath, json);
    }

    // ── Scalar Key-Value ───────────────────────────────────

    public static void Save(string key, string value)
    {
        lock (_lock)
        {
            var root = LoadAll();
            root[key] = value;
            SaveAll(root);
        }
    }

    public static string? Load(string key)
    {
        lock (_lock)
        {
            var root = LoadAll();
            return root.TryGetPropertyValue(key, out var node) ? node?.ToString() : null;
        }
    }

    public static bool Delete(string key)
    {
        lock (_lock)
        {
            var root = LoadAll();
            if (!root.Remove(key)) return false;
            SaveAll(root);
            return true;
        }
    }

    // ── Selected ASR Provider ──────────────────────────────

    private const string SelectedASRProviderKey = "tf_selectedASRProvider";

    public static string SelectedASRProvider
    {
        get => SettingsService.Get("selectedASRProvider") ?? "volcano";
        set => SettingsService.Set("selectedASRProvider", value);
    }

    // ── ASR Credentials ────────────────────────────────────

    private static string AsrStorageKey(string provider) => $"tf_asr_{provider}";

    public static void SaveASRCredentials(string provider, Dictionary<string, string> values)
    {
        lock (_lock)
        {
            var root = LoadAll();
            var obj = new JsonObject();
            foreach (var (k, v) in values)
                obj[k] = v;
            root[AsrStorageKey(provider)] = obj;
            SaveAll(root);
        }
    }

    public static Dictionary<string, string>? LoadASRCredentials(string provider)
    {
        lock (_lock)
        {
            var root = LoadAll();
            if (!root.TryGetPropertyValue(AsrStorageKey(provider), out var node))
                return null;
            if (node is not JsonObject obj) return null;

            var dict = new Dictionary<string, string>();
            foreach (var prop in obj)
            {
                if (prop.Value is not null)
                    dict[prop.Key] = prop.Value.ToString();
            }
            return dict.Count > 0 ? dict : null;
        }
    }

    // ── Selected LLM Provider ──────────────────────────────

    public static string SelectedLLMProvider
    {
        get => SettingsService.Get("selectedLLMProvider") ?? "doubao";
        set => SettingsService.Set("selectedLLMProvider", value);
    }

    // ── LLM Credentials ────────────────────────────────────

    private static string LlmStorageKey(string provider) => $"tf_llm_{provider}";

    public static void SaveLLMCredentials(string provider, Dictionary<string, string> values)
    {
        lock (_lock)
        {
            var root = LoadAll();
            var obj = new JsonObject();
            foreach (var (k, v) in values)
                obj[k] = v;
            root[LlmStorageKey(provider)] = obj;
            SaveAll(root);
        }
    }

    public static Dictionary<string, string>? LoadLLMCredentials(string provider)
    {
        lock (_lock)
        {
            var root = LoadAll();
            if (!root.TryGetPropertyValue(LlmStorageKey(provider), out var node))
                return null;
            if (node is not JsonObject obj) return null;

            var dict = new Dictionary<string, string>();
            foreach (var prop in obj)
            {
                if (prop.Value is not null)
                    dict[prop.Key] = prop.Value.ToString();
            }
            return dict.Count > 0 ? dict : null;
        }
    }

    // ── Migration ──────────────────────────────────────────

    /// <summary>
    /// Migrate legacy flat keys to provider-grouped format. Call once at app launch.
    /// </summary>
    public static void MigrateIfNeeded()
    {
        lock (_lock)
        {
            var root = LoadAll();
            bool migrated = false;

            // Migrate flat ASR keys → tf_asr_volcano
            if (root.TryGetPropertyValue("tf_appKey", out var appKeyNode)
                && appKeyNode?.ToString() is { Length: > 0 } appKey
                && !root.ContainsKey(AsrStorageKey("volcano")))
            {
                var accessKey = root["tf_accessKey"]?.ToString() ?? "";
                var resourceId = root["tf_resourceId"]?.ToString() ?? "volc.bigasr.sauc.duration";
                root[AsrStorageKey("volcano")] = new JsonObject
                {
                    ["appKey"] = appKey,
                    ["accessKey"] = accessKey,
                    ["resourceId"] = resourceId,
                };
                root.Remove("tf_appKey");
                root.Remove("tf_accessKey");
                root.Remove("tf_resourceId");
                migrated = true;
            }

            // Migrate flat LLM keys → tf_llm_doubao
            if (root.TryGetPropertyValue("tf_llmApiKey", out var llmKeyNode)
                && llmKeyNode?.ToString() is { Length: > 0 } llmApiKey
                && !root.ContainsKey(LlmStorageKey("doubao")))
            {
                var model = root["tf_llmModel"]?.ToString() ?? "";
                var baseURL = root["tf_llmBaseURL"]?.ToString() ?? "";
                root[LlmStorageKey("doubao")] = new JsonObject
                {
                    ["apiKey"] = llmApiKey,
                    ["model"] = model,
                    ["baseURL"] = string.IsNullOrEmpty(baseURL) ? "https://ark.cn-beijing.volces.com/api/v3" : baseURL,
                };
                root.Remove("tf_llmApiKey");
                root.Remove("tf_llmModel");
                root.Remove("tf_llmBaseURL");
                root.Remove("tf_llmEndpointId");
                migrated = true;
            }

            if (migrated)
                SaveAll(root);
        }
    }
}
