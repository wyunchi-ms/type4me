using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.Models;

namespace Type4Me.LLM.Providers;

/// <summary>
/// Tag interface for OpenAI-compatible LLM providers.
/// </summary>
public interface IOpenAICompatibleLLMTag
{
    static abstract LLMProvider Provider { get; }
}

// ── Tag Types (one per provider) ───────────────────────

public struct DoubaoTag      : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.Doubao; }
public struct MinimaxCNTag   : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.MinimaxCN; }
public struct MinimaxIntlTag : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.MinimaxIntl; }
public struct BailianTag     : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.Bailian; }
public struct KimiTag        : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.Kimi; }
public struct OpenRouterTag  : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.OpenRouter; }
public struct OpenAITag      : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.OpenAI; }
public struct GeminiTag      : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.Gemini; }
public struct DeepSeekTag    : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.DeepSeek; }
public struct ZhipuTag       : IOpenAICompatibleLLMTag { public static LLMProvider Provider => LLMProvider.Zhipu; }

/// <summary>
/// Generic config for all OpenAI-compatible LLM providers.
/// The TTag type parameter selects the provider.
/// </summary>
public sealed class OpenAICompatibleLLMConfig<TTag> where TTag : IOpenAICompatibleLLMTag
{
    public string ApiKey { get; }
    public string Model { get; }
    public string BaseURL { get; }

    public static LLMProvider Provider => TTag.Provider;

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "apiKey", Label = "API Key", Placeholder = "sk-...", IsSecure = true },
        new() { Key = "model", Label = Loc.L("模型", "Model"),
                Placeholder = Loc.L("模型名称或 endpoint ID", "Model name or endpoint ID") },
        new() { Key = "baseURL", Label = "Base URL",
                Placeholder = TTag.Provider.DefaultBaseURL(),
                IsOptional = true, DefaultValue = TTag.Provider.DefaultBaseURL() },
    ];

    private OpenAICompatibleLLMConfig(string apiKey, string model, string baseURL)
    {
        ApiKey = apiKey; Model = model; BaseURL = baseURL;
    }

    public static OpenAICompatibleLLMConfig<TTag>? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("apiKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("model", out var model) || string.IsNullOrEmpty(model)) return null;
        var baseURL = credentials.GetValueOrDefault("baseURL");
        if (string.IsNullOrEmpty(baseURL)) baseURL = TTag.Provider.DefaultBaseURL();
        return new OpenAICompatibleLLMConfig<TTag>(key, model, baseURL);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["apiKey"] = ApiKey, ["model"] = Model, ["baseURL"] = BaseURL,
    };

    public LLMConfig ToLLMConfig() => new(ApiKey, Model, BaseURL);
}
