using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Type4Me.ASR;
using Type4Me.Localization;
using Type4Me.Services;

namespace Type4Me.ViewModels;

/// <summary>
/// Setup wizard ViewModel — manages step progression and ASR credential entry.
/// </summary>
public partial class SetupWizardViewModel : ObservableObject
{
    public enum WizardStep { Welcome, QuickDemo, CustomDemo, Provider, Ready }

    [ObservableProperty] private WizardStep _currentStep = WizardStep.Welcome;
    [ObservableProperty] private ASRProvider _selectedASRProvider;
    [ObservableProperty] private Dictionary<string, string> _asrCredentials = new();
    [ObservableProperty] private string _statusMessage = string.Empty;

    public CredentialField[] ASRFields =>
        ASRProviderRegistry.GetEntry(SelectedASRProvider)?.GetCredentialFields() ?? [];

    /// <summary>True when the wizard reaches the final step and is dismissed.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Fired when the wizard is finished.</summary>
    public event Action? OnCompleted;

    public SetupWizardViewModel()
    {
        var asrRaw = CredentialService.SelectedASRProvider;
        SelectedASRProvider = ASRProviderExtensions.FromRawValue(asrRaw) ?? ASRProvider.Volcano;
        LoadASRCredentials();
    }

    private void LoadASRCredentials()
    {
        var creds = CredentialService.LoadASRCredentials(SelectedASRProvider.RawValue());
        AsrCredentials = creds ?? new Dictionary<string, string>();
    }

    partial void OnSelectedASRProviderChanged(ASRProvider value)
    {
        LoadASRCredentials();
        OnPropertyChanged(nameof(ASRFields));
    }

    [RelayCommand]
    public void NextStep()
    {
        CurrentStep = CurrentStep switch
        {
            WizardStep.Welcome => WizardStep.QuickDemo,
            WizardStep.QuickDemo => WizardStep.CustomDemo,
            WizardStep.CustomDemo => WizardStep.Provider,
            WizardStep.Provider => WizardStep.Ready,
            _ => CurrentStep,
        };
    }

    [RelayCommand]
    public void PreviousStep()
    {
        CurrentStep = CurrentStep switch
        {
            WizardStep.QuickDemo => WizardStep.Welcome,
            WizardStep.CustomDemo => WizardStep.QuickDemo,
            WizardStep.Provider => WizardStep.CustomDemo,
            WizardStep.Ready => WizardStep.Provider,
            _ => CurrentStep,
        };
    }

    [RelayCommand]
    public void SaveAndContinue()
    {
        // Save ASR credentials
        CredentialService.SelectedASRProvider = SelectedASRProvider.RawValue();
        CredentialService.SaveASRCredentials(SelectedASRProvider.RawValue(), AsrCredentials);
        StatusMessage = Loc.L("已保存", "Saved");
        NextStep();
    }

    [RelayCommand]
    public void Finish()
    {
        SettingsService.Set("hasCompletedSetup", true);
        IsCompleted = true;
        OnCompleted?.Invoke();
    }

    public static bool HasCompletedSetup => SettingsService.GetBool("hasCompletedSetup");
}
