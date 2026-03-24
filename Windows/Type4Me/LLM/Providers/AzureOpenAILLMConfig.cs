using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.Models;

namespace Type4Me.LLM.Providers;

/// <summary>
/// Azure OpenAI LLM provider configuration.
/// Uses api-key header auth + deployment-based URL routing.
/// Default model is gpt-5.2-chat which does not support temperature.
/// </summary>
public sealed class AzureOpenAILLMConfig
{
    public string ApiKey { get; }
    public string Endpoint { get; }
    public string DeploymentName { get; }
    public string ApiVersion { get; }

    public static LLMProvider Provider => LLMProvider.AzureOpenAI;

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "apiKey", Label = "API Key", IsSecure = true },
        new() { Key = "endpoint", Label = Loc.L("终结点", "Endpoint"),
                Placeholder = "https://{resource}.openai.azure.com" },
        new() { Key = "deploymentName", Label = Loc.L("部署名称", "Deployment Name"),
                Placeholder = "gpt-5.2-chat" },
        new() { Key = "apiVersion", Label = "API Version",
                Placeholder = "2025-04-01-preview",
                IsOptional = true, DefaultValue = "2025-04-01-preview" },
    ];

    private AzureOpenAILLMConfig(string apiKey, string endpoint, string deploymentName, string apiVersion)
    {
        ApiKey = apiKey;
        Endpoint = endpoint;
        DeploymentName = deploymentName;
        ApiVersion = apiVersion;
    }

    public static AzureOpenAILLMConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("apiKey", out var key) || string.IsNullOrEmpty(key)) return null;
        if (!credentials.TryGetValue("endpoint", out var endpoint) || string.IsNullOrEmpty(endpoint)) return null;
        if (!credentials.TryGetValue("deploymentName", out var deployment) || string.IsNullOrEmpty(deployment)) return null;

        var apiVersion = credentials.GetValueOrDefault("apiVersion");
        if (string.IsNullOrEmpty(apiVersion)) apiVersion = "2025-04-01-preview";

        return new AzureOpenAILLMConfig(key, endpoint.TrimEnd('/'), deployment, apiVersion);
    }

    public Dictionary<string, string> ToCredentials() => new()
    {
        ["apiKey"] = ApiKey,
        ["endpoint"] = Endpoint,
        ["deploymentName"] = DeploymentName,
        ["apiVersion"] = ApiVersion,
    };

    /// <summary>
    /// Builds the full chat completions URL:
    /// {endpoint}/openai/deployments/{deployment}/chat/completions?api-version={version}
    /// </summary>
    public string ChatCompletionsUrl =>
        $"{Endpoint}/openai/deployments/{DeploymentName}/chat/completions?api-version={ApiVersion}";

    public LLMConfig ToLLMConfig() => new(ApiKey, DeploymentName, Endpoint, ApiVersion);
}
