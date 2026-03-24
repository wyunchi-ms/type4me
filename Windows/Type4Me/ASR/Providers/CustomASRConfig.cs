using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class CustomASRConfig
{
    public string EndpointURL { get; }
    public string? ApiKey { get; }
    public string? SecretKey { get; }
    public string? AppId { get; }
    public string? Region { get; }

    public static ASRProvider Provider => ASRProvider.Custom;
    public static string DisplayName => Loc.L("自定义", "Custom");

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "endpointURL", Label = "Endpoint URL", Placeholder = Loc.L("wss://... 或 https://...", "wss://... or https://...") },
        new() { Key = "apiKey", Label = "API Key / Token", Placeholder = Loc.L("认证密钥", "Auth key"), IsSecure = true, IsOptional = true },
        new() { Key = "secretKey", Label = "Secret Key", Placeholder = Loc.L("可选", "Optional"), IsSecure = true, IsOptional = true },
        new() { Key = "appId", Label = "App ID", Placeholder = Loc.L("可选", "Optional"), IsOptional = true },
        new() { Key = "region", Label = "Region", Placeholder = Loc.L("可选", "Optional"), IsOptional = true },
    ];

    private CustomASRConfig(string endpointURL, string? apiKey, string? secretKey, string? appId, string? region)
    {
        EndpointURL = endpointURL; ApiKey = apiKey; SecretKey = secretKey; AppId = appId; Region = region;
    }

    public static CustomASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("endpointURL", out var url) || string.IsNullOrEmpty(url)) return null;
        return new CustomASRConfig(url,
            credentials.GetValueOrDefault("apiKey"),
            credentials.GetValueOrDefault("secretKey"),
            credentials.GetValueOrDefault("appId"),
            credentials.GetValueOrDefault("region"));
    }

    public Dictionary<string, string> ToCredentials()
    {
        var d = new Dictionary<string, string> { ["endpointURL"] = EndpointURL };
        if (!string.IsNullOrEmpty(ApiKey)) d["apiKey"] = ApiKey;
        if (!string.IsNullOrEmpty(SecretKey)) d["secretKey"] = SecretKey;
        if (!string.IsNullOrEmpty(AppId)) d["appId"] = AppId;
        if (!string.IsNullOrEmpty(Region)) d["region"] = Region;
        return d;
    }

    public bool IsValid => !string.IsNullOrEmpty(EndpointURL);
}
