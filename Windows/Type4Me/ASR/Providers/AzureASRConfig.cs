using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class AzureASRConfig
{
    public string SubscriptionKey { get; }
    public string Region { get; }
    public string Language { get; }
    public string? CustomEndpoint { get; }

    public static ASRProvider Provider => ASRProvider.Azure;
    public static string DisplayName => "Azure Speech";

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "subscriptionKey", Label = "Subscription Key", Placeholder = Loc.L("密钥", "Secret"), IsSecure = true },
        new() { Key = "region", Label = "Region", Placeholder = "eastasia" },
        new() { Key = "language", Label = Loc.L("语言", "Languages"), Placeholder = "zh-CN,en-US",
                IsOptional = true, DefaultValue = "zh-CN,en-US" },
        new() { Key = "customEndpoint", Label = "Custom Endpoint", Placeholder = Loc.L("可选", "Optional"), IsOptional = true },
    ];

    private AzureASRConfig(string subscriptionKey, string region, string language, string? customEndpoint)
    {
        SubscriptionKey = subscriptionKey; Region = region; Language = language; CustomEndpoint = customEndpoint;
    }

    public static AzureASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("subscriptionKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("region", out var region) || string.IsNullOrEmpty(region)) return null;
        var language = credentials.GetValueOrDefault("language");
        if (string.IsNullOrEmpty(language)) language = "zh-CN,en-US";
        return new AzureASRConfig(key, region, language, credentials.GetValueOrDefault("customEndpoint"));
    }

    public Dictionary<string, string> ToCredentials()
    {
        var d = new Dictionary<string, string>
        {
            ["subscriptionKey"] = SubscriptionKey,
            ["region"] = Region,
            ["language"] = Language,
        };
        if (!string.IsNullOrEmpty(CustomEndpoint)) d["customEndpoint"] = CustomEndpoint;
        return d;
    }

    public bool IsValid => !string.IsNullOrEmpty(SubscriptionKey) && !string.IsNullOrEmpty(Region);
}
