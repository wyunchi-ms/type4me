namespace Type4Me.ASR.Providers;

public sealed class OpenAIASRConfig
{
    public string ApiKey { get; }
    public string BaseURL { get; }

    public static ASRProvider Provider => ASRProvider.OpenAI;
    public static string DisplayName => "OpenAI Whisper";

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "apiKey", Label = "API Key", Placeholder = "sk-...", IsSecure = true },
        new() { Key = "baseURL", Label = "Base URL", Placeholder = "https://api.openai.com/v1", IsSecure = false, IsOptional = true, DefaultValue = "https://api.openai.com/v1" },
    ];

    private OpenAIASRConfig(string apiKey, string baseURL) { ApiKey = apiKey; BaseURL = baseURL; }

    public static OpenAIASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("apiKey", out var key) || string.IsNullOrEmpty(key)) return null;
        var baseURL = credentials.GetValueOrDefault("baseURL");
        if (string.IsNullOrEmpty(baseURL)) baseURL = "https://api.openai.com/v1";
        return new OpenAIASRConfig(key, baseURL);
    }

    public Dictionary<string, string> ToCredentials() => new() { ["apiKey"] = ApiKey, ["baseURL"] = BaseURL };
    public bool IsValid => !string.IsNullOrEmpty(ApiKey);
}
