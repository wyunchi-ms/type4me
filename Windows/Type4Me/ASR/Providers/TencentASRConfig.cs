using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class TencentASRConfig
{
    public string SecretId { get; }
    public string SecretKey { get; }
    public string AppId { get; }

    public static ASRProvider Provider => ASRProvider.Tencent;
    public static string DisplayName => Loc.L("腾讯云", "Tencent Cloud");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "secretId", Label = "Secret ID", Placeholder = Loc.L("密钥 ID", "Key ID") },
        new() { Key = "secretKey", Label = "Secret Key", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
        new() { Key = "appId", Label = "App ID", Placeholder = Loc.L("应用 ID", "App ID") },
    ];

    private TencentASRConfig(string secretId, string secretKey, string appId)
    {
        SecretId = secretId; SecretKey = secretKey; AppId = appId;
    }

    public static TencentASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("secretId", out var sid) || string.IsNullOrEmpty(sid)) return null;
        if (!credentials.TryGetValue("secretKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("appId", out var appId) || string.IsNullOrEmpty(appId)) return null;
        return new TencentASRConfig(sid, key, appId);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["secretId"] = SecretId, ["secretKey"] = SecretKey, ["appId"] = AppId,
    };

    public bool IsValid => !string.IsNullOrEmpty(SecretId) && !string.IsNullOrEmpty(SecretKey) && !string.IsNullOrEmpty(AppId);
}
