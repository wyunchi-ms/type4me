using Type4Me.Localization;

namespace Type4Me.ASR.Providers;

public sealed class GoogleASRConfig
{
    public string ServiceAccountJSON { get; }

    public static ASRProvider Provider => ASRProvider.Google;
    public static string DisplayName => "Google Cloud STT";

    public static CredentialField[] CredentialFields =>
    [
        new() { Key = "serviceAccountJSON", Label = "Service Account JSON",
                Placeholder = Loc.L("粘贴 JSON 内容或文件路径", "Paste JSON content or file path"), IsSecure = true },
    ];

    private GoogleASRConfig(string json) { ServiceAccountJSON = json; }

    public static GoogleASRConfig? TryCreate(Dictionary<string, string> credentials)
    {
        if (!credentials.TryGetValue("serviceAccountJSON", out var json) || string.IsNullOrEmpty(json)) return null;
        return new GoogleASRConfig(json);
    }

    public Dictionary<string, string> ToCredentials() => new() { ["serviceAccountJSON"] = ServiceAccountJSON };
    public bool IsValid => !string.IsNullOrEmpty(ServiceAccountJSON);
}
