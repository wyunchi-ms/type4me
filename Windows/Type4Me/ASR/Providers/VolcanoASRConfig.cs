using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class VolcanoASRConfig
{
    public string AppKey { get; }
    public string AccessKey { get; }
    public string ResourceId { get; }
    public string Uid { get; }

    public static ASRProvider Provider => ASRProvider.Volcano;
    public static string DisplayName => Loc.L("火山引擎 (Doubao)", "Volcano (Doubao)");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "appKey", Label = "App Key", Placeholder = "APPID", IsSecure = false, IsOptional = false },
        new() { Key = "accessKey", Label = "Access Token", Placeholder = Loc.L("访问令牌", "Access token"), IsSecure = true, IsOptional = false },
    ];

    private VolcanoASRConfig(string appKey, string accessKey, string resourceId, string uid)
    {
        AppKey = appKey;
        AccessKey = accessKey;
        ResourceId = resourceId;
        Uid = uid;
    }

    public static VolcanoASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("appKey", out var appKey) || string.IsNullOrEmpty(appKey))
            return null;
        if (!credentials.TryGetValue("accessKey", out var accessKey) || string.IsNullOrEmpty(accessKey))
            return null;

        var resourceId = credentials.GetValueOrDefault("resourceId");
        if (string.IsNullOrEmpty(resourceId))
            resourceId = "volc.bigasr.sauc.duration";

        var uid = Services.ASRCustomizationStorage.LoadOrCreateUID();

        return new VolcanoASRConfig(appKey, accessKey, resourceId, uid);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["appKey"] = AppKey,
        ["accessKey"] = AccessKey,
        ["resourceId"] = ResourceId,
    };

    public bool IsValid => !string.IsNullOrEmpty(AppKey) && !string.IsNullOrEmpty(AccessKey);
}
