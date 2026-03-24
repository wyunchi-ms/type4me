using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class AliyunASRConfig
{
    public string AccessKeyId { get; }
    public string AccessKeySecret { get; }
    public string AppKey { get; }

    public static ASRProvider Provider => ASRProvider.Aliyun;
    public static string DisplayName => Loc.L("阿里云", "Alibaba Cloud");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "accessKeyId", Label = "Access Key ID", Placeholder = Loc.L("密钥 ID", "Key ID") },
        new() { Key = "accessKeySecret", Label = "Access Key Secret", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
        new() { Key = "appKey", Label = "App Key", Placeholder = Loc.L("项目 AppKey", "Project AppKey") },
    ];

    private AliyunASRConfig(string accessKeyId, string accessKeySecret, string appKey)
    {
        AccessKeyId = accessKeyId; AccessKeySecret = accessKeySecret; AppKey = appKey;
    }

    public static AliyunASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("accessKeyId", out var kid) || string.IsNullOrEmpty(kid)) return null;
        if (!credentials.TryGetValue("accessKeySecret", out var secret) || string.IsNullOrEmpty(secret)) return null;
        if (!credentials.TryGetValue("appKey", out var appKey) || string.IsNullOrEmpty(appKey)) return null;
        return new AliyunASRConfig(kid, secret, appKey);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["accessKeyId"] = AccessKeyId, ["accessKeySecret"] = AccessKeySecret, ["appKey"] = AppKey,
    };

    public bool IsValid => !string.IsNullOrEmpty(AccessKeyId) && !string.IsNullOrEmpty(AccessKeySecret) && !string.IsNullOrEmpty(AppKey);
}
