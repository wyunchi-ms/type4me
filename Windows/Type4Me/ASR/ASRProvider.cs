using Type4Me.Localization;

namespace Type4Me.ASR;

/// <summary>
/// Options for ASR recognition requests.
/// </summary>
public sealed class ASRRequestOptions
{
    public bool EnablePunc { get; set; } = true;
    public string[] Hotwords { get; set; } = [];
    public string? BoostingTableID { get; set; }
    public int ContextHistoryLength { get; set; } = 20;
}

/// <summary>
/// Supported ASR providers.
/// </summary>
public enum ASRProvider
{
    OpenAI,
    Azure,
    Google,
    AWS,
    Volcano,
    Aliyun,
    Tencent,
    Iflytek,
    Custom,
}

public static class ASRProviderExtensions
{
    public static string RawValue(this ASRProvider provider) => provider switch
    {
        ASRProvider.OpenAI  => "openai",
        ASRProvider.Azure   => "azure",
        ASRProvider.Google  => "google",
        ASRProvider.AWS     => "aws",
        ASRProvider.Volcano => "volcano",
        ASRProvider.Aliyun  => "aliyun",
        ASRProvider.Tencent => "tencent",
        ASRProvider.Iflytek => "iflytek",
        ASRProvider.Custom  => "custom",
        _ => "custom",
    };

    public static string DisplayName(this ASRProvider provider) => provider switch
    {
        ASRProvider.OpenAI  => "OpenAI Whisper",
        ASRProvider.Azure   => "Azure Speech",
        ASRProvider.Google  => "Google Cloud STT",
        ASRProvider.AWS     => "AWS Transcribe",
        ASRProvider.Volcano => Loc.L("火山引擎 (Doubao)", "Volcano (Doubao)"),
        ASRProvider.Aliyun  => Loc.L("阿里云", "Alibaba Cloud"),
        ASRProvider.Tencent => Loc.L("腾讯云", "Tencent Cloud"),
        ASRProvider.Iflytek => Loc.L("讯飞", "iFLYTEK"),
        ASRProvider.Custom  => Loc.L("自定义", "Custom"),
        _ => "Unknown",
    };

    public static ASRProvider? FromRawValue(string value) => value switch
    {
        "openai"  => ASRProvider.OpenAI,
        "azure"   => ASRProvider.Azure,
        "google"  => ASRProvider.Google,
        "aws"     => ASRProvider.AWS,
        "volcano" => ASRProvider.Volcano,
        "aliyun"  => ASRProvider.Aliyun,
        "tencent" => ASRProvider.Tencent,
        "iflytek" => ASRProvider.Iflytek,
        "custom"  => ASRProvider.Custom,
        _ => null,
    };
}

/// <summary>
/// Describes a single credential input field for dynamic UI rendering.
/// </summary>
public sealed class CredentialField
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Placeholder { get; init; } = string.Empty;
    public bool IsSecure { get; init; }
    public bool IsOptional { get; init; }
    public string DefaultValue { get; init; } = string.Empty;
}

/// <summary>
/// Interface for ASR provider configuration — each provider implements this.
/// </summary>
public interface IASRProviderConfig
{
    static abstract ASRProvider Provider { get; }
    static abstract string DisplayName { get; }
    static abstract CredentialField[] CredentialFields { get; }

    Dictionary<string, string> ToCredentials();
    bool IsValid { get; }
}
