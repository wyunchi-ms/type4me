using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class IflytekASRConfig
{
    public string AppId { get; }
    public string ApiKey { get; }
    public string ApiSecret { get; }

    public static ASRProvider Provider => ASRProvider.Iflytek;
    public static string DisplayName => Loc.L("讯飞", "iFLYTEK");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "appId", Label = "App ID", Placeholder = Loc.L("应用 ID", "App ID") },
        new() { Key = "apiKey", Label = "API Key", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
        new() { Key = "apiSecret", Label = "API Secret", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
    ];

    private IflytekASRConfig(string appId, string apiKey, string apiSecret)
    {
        AppId = appId; ApiKey = apiKey; ApiSecret = apiSecret;
    }

    public static IflytekASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("appId", out var aid) || string.IsNullOrEmpty(aid)) return null;
        if (!credentials.TryGetValue("apiKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("apiSecret", out var secret) || string.IsNullOrEmpty(secret)) return null;
        return new IflytekASRConfig(aid, key, secret);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["appId"] = AppId, ["apiKey"] = ApiKey, ["apiSecret"] = ApiSecret,
    };

    public bool IsValid => !string.IsNullOrEmpty(AppId) && !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret);
}
