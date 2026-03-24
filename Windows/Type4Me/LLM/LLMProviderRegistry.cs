namespace Type4Me.LLM;

/// <summary>
/// Registry mapping LLM providers to their config types and client factories.
/// </summary>
public static class LLMProviderRegistry
{
    public sealed class ProviderEntry
    {
        public required Type ConfigType { get; init; }
        public required Func<ILLMClient> CreateClient { get; init; }

        public ASR.CredentialField[] GetCredentialFields()
        {
            var prop = ConfigType.GetProperty("CredentialFields",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return prop?.GetValue(null) as ASR.CredentialField[] ?? [];
        }

        public object? CreateConfig(Dictionary<string, string> credentials)
        {
            var method = ConfigType.GetMethod("TryCreate",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, [credentials]);
        }
    }

    private static readonly Dictionary<LLMProvider, ProviderEntry> _all = new()
    {
        [LLMProvider.Doubao]      = OpenAIEntry<Providers.DoubaoTag>(),
        [LLMProvider.MinimaxCN]   = OpenAIEntry<Providers.MinimaxCNTag>(),
        [LLMProvider.MinimaxIntl] = OpenAIEntry<Providers.MinimaxIntlTag>(),
        [LLMProvider.Bailian]     = OpenAIEntry<Providers.BailianTag>(),
        [LLMProvider.Kimi]        = OpenAIEntry<Providers.KimiTag>(),
        [LLMProvider.OpenRouter]  = OpenAIEntry<Providers.OpenRouterTag>(),
        [LLMProvider.OpenAI]      = OpenAIEntry<Providers.OpenAITag>(),
        [LLMProvider.AzureOpenAI] = new ProviderEntry
        {
            ConfigType = typeof(Providers.AzureOpenAILLMConfig),
            CreateClient = () => new AzureOpenAIChatClient(),
        },
        [LLMProvider.Gemini]      = OpenAIEntry<Providers.GeminiTag>(),
        [LLMProvider.DeepSeek]    = OpenAIEntry<Providers.DeepSeekTag>(),
        [LLMProvider.Zhipu]       = OpenAIEntry<Providers.ZhipuTag>(),
        [LLMProvider.Claude]      = new ProviderEntry
        {
            ConfigType = typeof(Providers.ClaudeLLMConfig),
            CreateClient = () => new ClaudeChatClient(),
        },
    };

    private static ProviderEntry OpenAIEntry<TTag>() where TTag : Providers.IOpenAICompatibleLLMTag => new()
    {
        ConfigType = typeof(Providers.OpenAICompatibleLLMConfig<TTag>),
        CreateClient = () => new OpenAIChatClient(),
    };

    public static ProviderEntry? GetEntry(LLMProvider provider) =>
        _all.TryGetValue(provider, out var entry) ? entry : null;

    public static Type? GetConfigType(LLMProvider provider) =>
        GetEntry(provider)?.ConfigType;

    public static ILLMClient? CreateClient(LLMProvider provider) =>
        GetEntry(provider)?.CreateClient();

    public static IEnumerable<LLMProvider> AllProviders => _all.Keys;
}
