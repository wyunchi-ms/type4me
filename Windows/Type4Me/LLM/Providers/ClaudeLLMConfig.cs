using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.Models;

namespace Type4Me.LLM.Providers;

/// <summary>
/// Claude-specific LLM provider configuration.
/// </summary>
public sealed class ClaudeLLMConfig
{
    public string ApiKey { get; }
    public string Model { get; }
    public string BaseURL { get; }
    public int MaxTokens { get; }

    public static LLMProvider Provider => LLMProvider.Claude;

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "apiKey", Label = "API Key", Placeholder = "sk-ant-...", IsSecure = true },
        new() { Key = "model", Label = Loc.L("模型", "Model"), Placeholder = "claude-sonnet-4-5-20250514" },
        new() { Key = "baseURL", Label = "Base URL",
                Placeholder = "https://api.anthropic.com/v1",
                IsOptional = true, DefaultValue = "https://api.anthropic.com/v1" },
    ];

    private ClaudeLLMConfig(string apiKey, string model, string baseURL, int maxTokens)
    {
        ApiKey = apiKey; Model = model; BaseURL = baseURL; MaxTokens = maxTokens;
    }

    public static ClaudeLLMConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("apiKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("model", out var model) || string.IsNullOrEmpty(model)) return null;
        var baseURL = credentials.GetValueOrDefault("baseURL");
        if (string.IsNullOrEmpty(baseURL)) baseURL = LLMProvider.Claude.DefaultBaseURL();
        int.TryParse(credentials.GetValueOrDefault("maxTokens"), out var maxTokens);
        if (maxTokens <= 0) maxTokens = 4096;
        return new ClaudeLLMConfig(key, model, baseURL, maxTokens);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["apiKey"] = ApiKey, ["model"] = Model, ["baseURL"] = BaseURL,
    };

    public LLMConfig ToLLMConfig() => new(ApiKey, Model, BaseURL);
}
