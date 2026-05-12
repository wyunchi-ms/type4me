using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class VolcanoASRConfig
{
    public const string DefaultResourceId = "volc.bigasr.sauc.duration";
    public const string FlashResourceId = "volc.bigasr.auc_turbo";

    public string ApiKey { get; }
    public string ResourceId { get; }
    public string Uid { get; }

    public static ASRProvider Provider => ASRProvider.Volcano;
    public static string DisplayName => Loc.L("火山引擎 (Doubao)", "Volcano (Doubao)");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "apiKey", Label = "API Key", Placeholder = Loc.L("新版控制台 API Key", "API Key from new console"), IsSecure = true, IsOptional = false },
        new() { Key = "resourceId", Label = Loc.L("资源 ID", "Resource ID"), Placeholder = DefaultResourceId, IsSecure = false, IsOptional = true },
    ];

    private VolcanoASRConfig(string apiKey, string resourceId, string uid)
    {
        ApiKey = apiKey;
        ResourceId = resourceId;
        Uid = uid;
    }

    public static VolcanoASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        // New-console single API key. Accept legacy "accessKey" as a fallback for migration.
        if (!credentials.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            credentials.TryGetValue("accessKey", out apiKey);
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var resourceId = credentials.GetValueOrDefault("resourceId");
        if (string.IsNullOrWhiteSpace(resourceId))
            resourceId = DefaultResourceId;

        var uid = Services.ASRCustomizationStorage.LoadOrCreateUID();

        return new VolcanoASRConfig(apiKey.Trim(), resourceId.Trim(), uid);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["apiKey"] = ApiKey,
        ["resourceId"] = ResourceId,
    };

    public bool IsValid => !string.IsNullOrEmpty(ApiKey);
}
