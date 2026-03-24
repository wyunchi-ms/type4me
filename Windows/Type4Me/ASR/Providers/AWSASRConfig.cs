using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class AWSASRConfig
{
    public string AccessKeyId { get; }
    public string SecretAccessKey { get; }
    public string Region { get; }
    public string? SessionToken { get; }

    public static ASRProvider Provider => ASRProvider.AWS;
    public static string DisplayName => "AWS Transcribe";

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "accessKeyId", Label = "Access Key ID", Placeholder = "AKIA..." },
        new() { Key = "secretAccessKey", Label = "Secret Access Key", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
        new() { Key = "region", Label = "Region", Placeholder = "us-east-1" },
        new() { Key = "sessionToken", Label = "Session Token", Placeholder = Loc.L("可选", "Optional"), IsSecure = true, IsOptional = true },
    ];

    private AWSASRConfig(string accessKeyId, string secretAccessKey, string region, string? sessionToken)
    {
        AccessKeyId = accessKeyId; SecretAccessKey = secretAccessKey; Region = region; SessionToken = sessionToken;
    }

    public static AWSASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("accessKeyId", out var kid) || string.IsNullOrEmpty(kid)) return null;
        if (!credentials.TryGetValue("secretAccessKey", out var secret) || string.IsNullOrEmpty(secret)) return null;
        if (!credentials.TryGetValue("region", out var region) || string.IsNullOrEmpty(region)) return null;
        return new AWSASRConfig(kid, secret, region, credentials.GetValueOrDefault("sessionToken"));
    }

    public Dictionary<string, string> ToCredentials()
    {
        var d = new Dictionary<string, string>
        {
            ["accessKeyId"] = AccessKeyId, ["secretAccessKey"] = SecretAccessKey, ["region"] = Region,
        };
        if (!string.IsNullOrEmpty(SessionToken)) d["sessionToken"] = SessionToken;
        return d;
    }

    public bool IsValid => !string.IsNullOrEmpty(AccessKeyId) && !string.IsNullOrEmpty(SecretAccessKey) && !string.IsNullOrEmpty(Region);
}
