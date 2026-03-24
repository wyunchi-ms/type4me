using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.Models;

namespace Type4Me.LLM;

/// <summary>
/// Supported LLM providers.
/// </summary>
public enum LLMProvider
{
    Doubao,
    MinimaxCN,
    MinimaxIntl,
    Bailian,
    Kimi,
    OpenRouter,
    OpenAI,
    AzureOpenAI,
    Gemini,
    DeepSeek,
    Zhipu,
    Claude,
}

public static class LLMProviderExtensions
{
    public static string RawValue(this LLMProvider provider) => provider switch
    {
        LLMProvider.Doubao      => "doubao",
        LLMProvider.MinimaxCN   => "minimaxCN",
        LLMProvider.MinimaxIntl => "minimaxIntl",
        LLMProvider.Bailian     => "bailian",
        LLMProvider.Kimi        => "kimi",
        LLMProvider.OpenRouter  => "openrouter",
        LLMProvider.OpenAI      => "openai",
        LLMProvider.AzureOpenAI => "azureOpenAI",
        LLMProvider.Gemini      => "gemini",
        LLMProvider.DeepSeek    => "deepseek",
        LLMProvider.Zhipu       => "zhipu",
        LLMProvider.Claude      => "claude",
        _ => "doubao",
    };

    public static string DisplayName(this LLMProvider provider) => provider switch
    {
        LLMProvider.Doubao      => Loc.L("豆包 (ByteDance ARK)", "Doubao (ByteDance ARK)"),
        LLMProvider.MinimaxCN   => Loc.L("MiniMax 国内", "MiniMax China"),
        LLMProvider.MinimaxIntl => Loc.L("MiniMax 海外", "MiniMax Global"),
        LLMProvider.Bailian     => Loc.L("百炼 (阿里云)", "Bailian (Alibaba Cloud)"),
        LLMProvider.Kimi        => Loc.L("Kimi (月之暗面)", "Kimi (Moonshot)"),
        LLMProvider.OpenRouter  => "OpenRouter",
        LLMProvider.OpenAI      => "OpenAI",
        LLMProvider.AzureOpenAI => "Azure OpenAI",
        LLMProvider.Gemini      => "Gemini (Google)",
        LLMProvider.DeepSeek    => Loc.L("DeepSeek (深度求索)", "DeepSeek"),
        LLMProvider.Zhipu       => Loc.L("智谱 (GLM)", "Zhipu (GLM)"),
        LLMProvider.Claude      => "Claude (Anthropic)",
        _ => "Unknown",
    };

    public static string DefaultBaseURL(this LLMProvider provider) => provider switch
    {
        LLMProvider.Doubao      => "https://ark.cn-beijing.volces.com/api/v3",
        LLMProvider.MinimaxCN   => "https://api.minimax.chat/v1",
        LLMProvider.MinimaxIntl => "https://api.minimax.io/v1",
        LLMProvider.Bailian     => "https://dashscope.aliyuncs.com/compatible-mode/v1",
        LLMProvider.Kimi        => "https://api.moonshot.ai/v1",
        LLMProvider.OpenRouter  => "https://openrouter.ai/api/v1",
        LLMProvider.OpenAI      => "https://api.openai.com/v1",
        LLMProvider.AzureOpenAI => "",
        LLMProvider.Gemini      => "https://generativelanguage.googleapis.com/v1beta/openai",
        LLMProvider.DeepSeek    => "https://api.deepseek.com",
        LLMProvider.Zhipu       => "https://open.bigmodel.cn/api/paas/v4",
        LLMProvider.Claude      => "https://api.anthropic.com/v1",
        _ => "",
    };

    public static bool IsOpenAICompatible(this LLMProvider provider) =>
        provider != LLMProvider.Claude && provider != LLMProvider.AzureOpenAI;

    public static LLMProvider? FromRawValue(string value) => value switch
    {
        "doubao"      => LLMProvider.Doubao,
        "minimaxCN"   => LLMProvider.MinimaxCN,
        "minimaxIntl" => LLMProvider.MinimaxIntl,
        "bailian"     => LLMProvider.Bailian,
        "kimi"        => LLMProvider.Kimi,
        "openrouter"  => LLMProvider.OpenRouter,
        "openai"      => LLMProvider.OpenAI,
        "azureOpenAI" => LLMProvider.AzureOpenAI,
        "gemini"      => LLMProvider.Gemini,
        "deepseek"    => LLMProvider.DeepSeek,
        "zhipu"       => LLMProvider.Zhipu,
        "claude"      => LLMProvider.Claude,
        _ => null,
    };
}

/// <summary>
/// Interface for LLM provider configuration.
/// </summary>
public interface ILLMProviderConfig
{
    static abstract LLMProvider Provider { get; }
    static abstract CredentialField[] CredentialFields { get; }

    Dictionary<string, string> ToCredentials();
    LLMConfig ToLLMConfig();
}
