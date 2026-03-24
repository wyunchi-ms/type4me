namespace Type4Me.ASR;

/// <summary>
/// Registry mapping ASR providers to their config types and client factories.
/// </summary>
public static class ASRProviderRegistry
{
    public sealed class ProviderEntry
    {
        /// <summary>Config type for this provider.</summary>
        public required Type ConfigType { get; init; }

        /// <summary>Factory to create the speech recognizer, or null if not yet implemented.</summary>
        public Func<ISpeechRecognizer>? CreateClient { get; init; }

        /// <summary>Whether a client implementation is available.</summary>
        public bool IsAvailable => CreateClient != null;

        /// <summary>Get credential fields from the config type.</summary>
        public CredentialField[] GetCredentialFields()
        {
            var prop = ConfigType.GetProperty("CredentialFields",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return prop?.GetValue(null) as CredentialField[] ?? [];
        }

        /// <summary>Try to create a config from credential dict.</summary>
        public object? CreateConfig(Dictionary<string, string> credentials)
        {
            var method = ConfigType.GetMethod("TryCreate",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return method?.Invoke(null, [credentials]);
        }
    }

    private static readonly Dictionary<ASRProvider, ProviderEntry> _all = new()
    {
        [ASRProvider.Volcano] = new ProviderEntry
        {
            ConfigType = typeof(Providers.VolcanoASRConfig),
            CreateClient = () => new VolcASRClient(),
        },
        [ASRProvider.OpenAI] = new ProviderEntry
        {
            ConfigType = typeof(Providers.OpenAIASRConfig),
            CreateClient = null,
        },
        [ASRProvider.Azure] = new ProviderEntry
        {
            ConfigType = typeof(Providers.AzureASRConfig),
            CreateClient = () => new AzureASRClient(),
        },
        [ASRProvider.Google] = new ProviderEntry
        {
            ConfigType = typeof(Providers.GoogleASRConfig),
            CreateClient = null,
        },
        [ASRProvider.AWS] = new ProviderEntry
        {
            ConfigType = typeof(Providers.AWSASRConfig),
            CreateClient = null,
        },
        [ASRProvider.Aliyun] = new ProviderEntry
        {
            ConfigType = typeof(Providers.AliyunASRConfig),
            CreateClient = null,
        },
        [ASRProvider.Tencent] = new ProviderEntry
        {
            ConfigType = typeof(Providers.TencentASRConfig),
            CreateClient = null,
        },
        [ASRProvider.Iflytek] = new ProviderEntry
        {
            ConfigType = typeof(Providers.IflytekASRConfig),
            CreateClient = null,
        },
        [ASRProvider.Custom] = new ProviderEntry
        {
            ConfigType = typeof(Providers.CustomASRConfig),
            CreateClient = null,
        },
    };

    public static ProviderEntry? GetEntry(ASRProvider provider) =>
        _all.TryGetValue(provider, out var entry) ? entry : null;

    public static Type? GetConfigType(ASRProvider provider) =>
        GetEntry(provider)?.ConfigType;

    public static ISpeechRecognizer? CreateClient(ASRProvider provider) =>
        GetEntry(provider)?.CreateClient?.Invoke();

    public static IEnumerable<ASRProvider> AllProviders => _all.Keys;
}
