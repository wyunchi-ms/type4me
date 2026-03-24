using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Type4Me.ASR;
using Type4Me.LLM;
using Type4Me.Localization;
using Type4Me.Services;

namespace Type4Me.ViewModels;

/// <summary>
/// General settings tab: ASR/LLM provider selection, credentials, preferences.
/// </summary>
public partial class GeneralSettingsViewModel : ObservableObject
{
    [ObservableProperty] private ASRProvider _selectedASRProvider;
    [ObservableProperty] private LLMProvider _selectedLLMProvider;
    [ObservableProperty] private Dictionary<string, string> _asrCredentials = new();
    [ObservableProperty] private Dictionary<string, string> _llmCredentials = new();
    [ObservableProperty] private bool _enableSound = true;
    [ObservableProperty] private bool _launchAtLogin;
    [ObservableProperty] private AppLanguage _language;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public CredentialField[] ASRFields =>
        ASRProviderRegistry.GetEntry(SelectedASRProvider)?.GetCredentialFields() ?? [];

    public CredentialField[] LLMFields =>
        LLMProviderRegistry.GetEntry(SelectedLLMProvider)?.GetCredentialFields() ?? [];

    public GeneralSettingsViewModel()
    {
        var asrRaw = CredentialService.SelectedASRProvider;
        SelectedASRProvider = ASRProviderExtensions.FromRawValue(asrRaw) ?? ASRProvider.Volcano;

        var llmRaw = CredentialService.SelectedLLMProvider;
        SelectedLLMProvider = LLMProviderExtensions.FromRawValue(llmRaw) ?? LLMProvider.Doubao;

        LoadASRCredentials();
        LoadLLMCredentials();

        EnableSound = SettingsService.GetBool("enableSound", true);
        LaunchAtLogin = SettingsService.GetBool("launchAtLogin");
        Language = Loc.Current;
    }

    private void LoadASRCredentials()
    {
        var creds = CredentialService.LoadASRCredentials(SelectedASRProvider.RawValue());
        AsrCredentials = creds ?? new Dictionary<string, string>();
    }

    private void LoadLLMCredentials()
    {
        var creds = CredentialService.LoadLLMCredentials(SelectedLLMProvider.RawValue());
        LlmCredentials = creds ?? new Dictionary<string, string>();
    }

    [RelayCommand]
    public void SaveASRCredentials()
    {
        CredentialService.SelectedASRProvider = SelectedASRProvider.RawValue();
        CredentialService.SaveASRCredentials(SelectedASRProvider.RawValue(), AsrCredentials);
        StatusMessage = Loc.L("ASR 凭证已保存", "ASR credentials saved");
    }

    [RelayCommand]
    public void SaveLLMCredentials()
    {
        CredentialService.SelectedLLMProvider = SelectedLLMProvider.RawValue();
        CredentialService.SaveLLMCredentials(SelectedLLMProvider.RawValue(), LlmCredentials);
        StatusMessage = Loc.L("LLM 凭证已保存", "LLM credentials saved");
    }

    partial void OnSelectedASRProviderChanged(ASRProvider value)
    {
        LoadASRCredentials();
        OnPropertyChanged(nameof(ASRFields));
    }

    partial void OnSelectedLLMProviderChanged(LLMProvider value)
    {
        LoadLLMCredentials();
        OnPropertyChanged(nameof(LLMFields));
    }

    partial void OnLanguageChanged(AppLanguage value)
    {
        Loc.Current = value;
        SettingsService.Set("language", value.ToStorageValue());
    }
}
