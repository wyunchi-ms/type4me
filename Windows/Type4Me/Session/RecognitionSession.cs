using Type4Me.ASR;
using Type4Me.ASR.Providers;
using Type4Me.Audio;
using Type4Me.Database;
using Type4Me.Injection;
using Type4Me.LLM;
using Type4Me.Localization;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.Session;

/// <summary>
/// Core state machine: idle → starting → recording → finishing → injecting → idle.
/// Thread-safe via SemaphoreSlim (mirrors the Swift actor).
/// </summary>
public sealed class RecognitionSession : IDisposable
{
    // ── State ──────────────────────────────────────────────

    public enum SessionState { Idle, Starting, Recording, Finishing, Injecting, PostProcessing }

    private SessionState _state = SessionState.Idle;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SessionState State => _state;
    public bool CanStartRecording => _state == SessionState.Idle;

    // ── Dependencies ───────────────────────────────────────

    private readonly AudioCaptureEngine _audioEngine = new();
    private readonly TextInjectionEngine _injectionEngine = new();
    public readonly HistoryStore HistoryStore = new();
    private ISpeechRecognizer? _asrClient;

    // ── Mode & Timing ──────────────────────────────────────

    private ProcessingMode _currentMode = ProcessingMode.Direct;
    private DateTime? _recordingStartTime;
    private Dictionary<string, string>? _currentCredentials;

    // ── Callbacks ──────────────────────────────────────────

    public Action<RecognitionEvent>? OnASREvent { get; set; }
    public Action<float>? OnAudioLevel { get; set; }
    public Action<string, string, string?>? OnDebugLog { get; set; }

    // ── Accumulated text ───────────────────────────────────

    private RecognitionTranscript _currentTranscript = new();
    private CancellationTokenSource? _eventCts;
    private Task? _eventConsumptionTask;
    private Task<string?>? _activeFlashTask;
    private bool _hasEmittedReady;

    // ── Speculative LLM ────────────────────────────────────

    private Task<string?>? _speculativeLLMTask;
    private string _speculativeLLMText = "";
    private CancellationTokenSource? _speculativeDebounceCts;

    // ── Warm-up ────────────────────────────────────────────

    public void WarmUp() => _audioEngine.WarmUp();

    // ── Toggle ─────────────────────────────────────────────

    public async Task ToggleRecordingAsync()
    {
        await _lock.WaitAsync();
        try
        {
            switch (_state)
            {
                case SessionState.Idle:
                    await StartRecordingInternalAsync(ProcessingMode.Direct);
                    break;
                case SessionState.Recording:
                    await StopRecordingInternalAsync();
                    break;
            }
        }
        finally { _lock.Release(); }
    }

    // ── Start ──────────────────────────────────────────────

    public async Task StartRecordingAsync(ProcessingMode mode)
    {
        await _lock.WaitAsync();
        try
        {
            await StartRecordingInternalAsync(mode);
        }
        finally { _lock.Release(); }
    }

    private async Task StartRecordingInternalAsync(ProcessingMode mode)
    {
        if (_state != SessionState.Idle)
        {
            DebugFileLogger.Log($"[Session] Forcing reset from state={_state}");
            await ForceResetAsync();
        }

        _currentMode = mode;
        _recordingStartTime = null;
        _hasEmittedReady = false;
        _state = SessionState.Starting;

        // Load credentials
        var providerRaw = CredentialService.SelectedASRProvider;
        var provider = ASRProviderExtensions.FromRawValue(providerRaw) ?? ASRProvider.Volcano;
        var credentials = CredentialService.LoadASRCredentials(providerRaw);

        if (credentials == null)
        {
            SoundFeedback.PlayError();
            _state = SessionState.Idle;
            OnASREvent?.Invoke(new RecognitionEvent.Error(
                new InvalidOperationException(Loc.L("未配置 API 凭证", "API credentials not configured"))));
            OnASREvent?.Invoke(new RecognitionEvent.Completed());
            return;
        }

        _currentCredentials = credentials;

        var client = ASRProviderRegistry.CreateClient(provider);
        if (client == null)
        {
            SoundFeedback.PlayError();
            _state = SessionState.Idle;
            OnASREvent?.Invoke(new RecognitionEvent.Error(
                new InvalidOperationException(Loc.L($"{provider.DisplayName()} 暂不支持", $"{provider.DisplayName()} not yet supported"))));
            OnASREvent?.Invoke(new RecognitionEvent.Completed());
            return;
        }
        _asrClient = client;

        // Load hotwords and options
        var hotwords = HotwordStorage.Load();
        var needsLLM = !string.IsNullOrEmpty(mode.Prompt);
        var options = new ASRRequestOptions
        {
            EnablePunc = !needsLLM,
            Hotwords = hotwords,
            BoostingTableID = ASRCustomizationStorage.GetBoostingTableID(),
            ContextHistoryLength = ASRCustomizationStorage.GetContextHistoryLength(),
        };

        try
        {
            await client.ConnectAsync(credentials, options);
            DebugFileLogger.Log($"[Session] ASR connected OK (hotwords={hotwords.Length})");
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[Session] ASR connect FAILED: {ex.Message}");
            SoundFeedback.PlayError();
            await client.DisconnectAsync();
            _asrClient = null;
            _state = SessionState.Idle;
            OnASREvent?.Invoke(new RecognitionEvent.Error(ex));
            OnASREvent?.Invoke(new RecognitionEvent.Completed());
            return;
        }

        _currentTranscript = new RecognitionTranscript();

        // Start event consumption
        _eventCts = new CancellationTokenSource();
        _eventConsumptionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in client.Events.ReadAllAsync(_eventCts.Token))
                {
                    HandleASREvent(evt);
                    if (evt is RecognitionEvent.Completed) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wire audio level → UI
        _audioEngine.OnAudioLevel = level => OnAudioLevel?.Invoke(level);

        // Wire audio → ASR
        int chunkCount = 0;
        _audioEngine.OnAudioChunk = data =>
        {
            chunkCount++;
            if (chunkCount == 1)
            {
                DebugFileLogger.Log($"[Session] First audio chunk: {data.Length} bytes");
                MarkReadyIfNeeded();
            }
            _ = Task.Run(async () =>
            {
                try { await _asrClient?.SendAudioAsync(data)!; }
                catch { /* ignore send errors during shutdown */ }
            });
        };

        try
        {
            _audioEngine.Start();
            DebugFileLogger.Log("[Session] Audio engine started OK");
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[Session] Audio engine start FAILED: {ex.Message}");
            SoundFeedback.PlayError();
            await client.DisconnectAsync();
            _asrClient = null;
            _state = SessionState.Idle;
            OnASREvent?.Invoke(new RecognitionEvent.Error(ex));
            return;
        }

        _state = SessionState.Recording;

        // Pre-warm LLM if needed
        if (!string.IsNullOrEmpty(_currentMode.Prompt))
        {
            var llmProviderRaw = CredentialService.SelectedLLMProvider;
            var llmProvider = LLMProviderExtensions.FromRawValue(llmProviderRaw);
            if (llmProvider.HasValue)
            {
                var llmClient = LLMProviderRegistry.CreateClient(llmProvider.Value);
                var llmCreds = CredentialService.LoadLLMCredentials(llmProviderRaw);
                if (llmClient != null && llmCreds != null)
                {
                    var entry = LLMProviderRegistry.GetEntry(llmProvider.Value);
                    var cfg = entry?.CreateConfig(llmCreds);
                    if (cfg != null)
                    {
                        var toLLM = cfg.GetType().GetMethod("ToLLMConfig");
                        if (toLLM?.Invoke(cfg, null) is LLMConfig llmConfig)
                            _ = Task.Run(() => llmClient.WarmUpAsync(llmConfig.BaseURL));
                    }
                }
            }
        }
    }

    public void SwitchMode(ProcessingMode mode) => _currentMode = mode;

    // ── Stop ───────────────────────────────────────────────

    public async Task StopRecordingAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await StopRecordingInternalAsync();
        }
        finally { _lock.Release(); }
    }

    private async Task StopRecordingInternalAsync()
    {
        if (_state != SessionState.Recording)
        {
            DebugFileLogger.Log($"[Session] stopRecording called but state is {_state}");
            return;
        }

        SoundFeedback.PlayStop();
        _state = SessionState.Finishing;

        // Stop audio
        _audioEngine.OnAudioChunk = null;
        _audioEngine.Stop();

        CancelSpeculativeLLM();
        bool isPerformanceMode = _currentMode.Id == ProcessingMode.PerformanceId;
        bool needsLLM = !string.IsNullOrEmpty(_currentMode.Prompt) && !isPerformanceMode;

        // Teardown ASR
        if (_asrClient != null)
        {
            if (needsLLM)
            {
                // Fast disconnect for LLM modes
                _eventCts?.Cancel();
                await _asrClient.DisconnectAsync();
            }
            else
            {
                // Full teardown for direct/performance modes
                try
                {
                    using var endCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _asrClient.EndAudioAsync(endCts.Token);
                }
                catch { /* timeout or error */ }

                _eventCts?.Cancel();
                if (_eventConsumptionTask != null)
                {
                    try { await _eventConsumptionTask.WaitAsync(TimeSpan.FromSeconds(1)); }
                    catch { /* timeout */ }
                }
                await _asrClient.DisconnectAsync();
            }
        }
        _eventConsumptionTask = null;
        _asrClient?.Dispose();
        _asrClient = null;
        _hasEmittedReady = false;

        // Get text
        var streamingText = _currentTranscript.DisplayText;

        // Dual-channel flash ASR for performance mode
        var effectiveText = streamingText;
        if (isPerformanceMode && _currentCredentials != null)
        {
            var volcConfig = VolcanoASRConfig.TryCreate(_currentCredentials);
            if (volcConfig != null)
            {
                var pcmData = _audioEngine.GetRecordedAudio();
                if (pcmData.Length > 0)
                {
                    try
                    {
                        var flashText = await VolcFlashASRClient.RecognizeAsync(pcmData, volcConfig);
                        if (!string.IsNullOrEmpty(flashText))
                            effectiveText = flashText;
                    }
                    catch (Exception ex)
                    {
                        DebugFileLogger.Log($"[Session] Flash ASR failed: {ex.Message}");
                    }
                }
            }
        }
        _currentCredentials = null;

        if (!string.IsNullOrEmpty(effectiveText))
        {
            var rawText = effectiveText;
            var finalText = SnippetStorage.Apply(effectiveText);
            string? processedText = null;

            // LLM post-processing
            if (needsLLM)
            {
                _state = SessionState.PostProcessing;
                var llmConfig = LoadCurrentLLMConfig();
                if (llmConfig != null)
                {
                    var requestBody = _currentMode.Prompt.Replace("{text}", finalText);
                    OnDebugLog?.Invoke("LLM", $"→ Request  [{llmConfig.Model}]", requestBody);
                    try
                    {
                        var llmClient = CreateCurrentLLMClient();
                        var result = await llmClient.ProcessAsync(finalText, _currentMode.Prompt, llmConfig);
                        OnDebugLog?.Invoke("LLM", $"← Response ({result.Length} chars)", result);
                        processedText = result;
                        finalText = result;
                        OnASREvent?.Invoke(new RecognitionEvent.ProcessingResult(result));
                    }
                    catch (Exception ex)
                    {
                        OnDebugLog?.Invoke("ERROR", $"LLM failed: {ex.Message}", ex.ToString());
                        OnASREvent?.Invoke(new RecognitionEvent.ProcessingResult(rawText));
                    }
                }
                else
                {
                    OnASREvent?.Invoke(new RecognitionEvent.ProcessingResult(rawText));
                }
            }

            _state = SessionState.Injecting;
            var outcome = _injectionEngine.Inject(finalText);
            OnASREvent?.Invoke(new RecognitionEvent.Finalized(finalText, outcome));

            // Save to history
            var duration = _recordingStartTime.HasValue
                ? (DateTime.Now - _recordingStartTime.Value).TotalSeconds
                : 0;
            await HistoryStore.InsertAsync(new HistoryRecord
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.Now,
                DurationSeconds = duration,
                RawText = rawText,
                ProcessingMode = _currentMode.Id == ProcessingMode.DirectId ? null : _currentMode.Name,
                ProcessedText = processedText,
                FinalText = finalText,
                Status = "completed",
            });
        }
        else
        {
            OnASREvent?.Invoke(new RecognitionEvent.ProcessingResult(""));
        }

        if (_state == SessionState.Finishing || _state == SessionState.PostProcessing || _state == SessionState.Injecting)
        {
            _state = SessionState.Idle;
            _hasEmittedReady = false;
            _currentTranscript = new RecognitionTranscript();
        }
        ResetSpeculativeLLM();
    }

    // ── ASR Event Handling ─────────────────────────────────

    private void HandleASREvent(RecognitionEvent evt)
    {
        OnASREvent?.Invoke(evt);

        switch (evt)
        {
            case RecognitionEvent.Transcript t:
                _currentTranscript = t.Value;
                if (_state == SessionState.Recording && !string.IsNullOrEmpty(_currentMode.Prompt))
                    ScheduleSpeculativeLLM();
                break;

            case RecognitionEvent.Completed when _state == SessionState.Recording:
                DebugFileLogger.Log("[Session] Server closed ASR while recording, initiating stop");
                _ = Task.Run(StopRecordingAsync);
                break;
        }
    }

    private void MarkReadyIfNeeded()
    {
        if (_hasEmittedReady) return;
        _hasEmittedReady = true;
        _recordingStartTime = DateTime.Now;
        DebugFileLogger.Log("[Session] Emitting ready");
        OnASREvent?.Invoke(new RecognitionEvent.Ready());
    }

    // ── Speculative LLM ────────────────────────────────────

    private void ScheduleSpeculativeLLM()
    {
        _speculativeDebounceCts?.Cancel();
        _speculativeDebounceCts = new CancellationTokenSource();
        var ct = _speculativeDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800, ct);
                if (_state != SessionState.Recording) return;
                await FireSpeculativeLLMAsync();
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task FireSpeculativeLLMAsync()
    {
        var text = SnippetStorage.Apply(_currentTranscript.ComposedText.Trim());
        if (string.IsNullOrEmpty(text) || text == _speculativeLLMText) return;

        var llmConfig = LoadCurrentLLMConfig();
        if (llmConfig == null) return;

        _speculativeLLMText = text;
        var prompt = _currentMode.Prompt;
        var client = CreateCurrentLLMClient();

        _speculativeLLMTask = Task.Run<string?>(async () =>
        {
            try
            {
                return await client.ProcessAsync(text, prompt, llmConfig);
            }
            catch { return null; }
        });
    }

    private void CancelSpeculativeLLM()
    {
        _speculativeDebounceCts?.Cancel();
        _speculativeDebounceCts = null;
    }

    private void ResetSpeculativeLLM()
    {
        CancelSpeculativeLLM();
        _speculativeLLMTask = null;
        _speculativeLLMText = "";
    }

    // ── Force Reset ────────────────────────────────────────

    private async Task ForceResetAsync()
    {
        DebugFileLogger.Log($"[Session] ForceReset from state={_state}");
        _eventCts?.Cancel();
        _eventConsumptionTask = null;
        ResetSpeculativeLLM();

        _audioEngine.OnAudioChunk = null;
        _audioEngine.Stop();
        _audioEngine.OnAudioLevel = null;

        if (_asrClient != null)
            await _asrClient.DisconnectAsync();
        _asrClient?.Dispose();
        _asrClient = null;

        _state = SessionState.Idle;
        _currentTranscript = new RecognitionTranscript();
        _hasEmittedReady = false;
        _currentCredentials = null;
    }

    // ── Helpers ─────────────────────────────────────────────

    private LLMConfig? LoadCurrentLLMConfig()
    {
        var providerRaw = CredentialService.SelectedLLMProvider;
        var provider = LLMProviderExtensions.FromRawValue(providerRaw);
        if (provider == null) return null;

        var creds = CredentialService.LoadLLMCredentials(providerRaw);
        if (creds == null) return null;

        var entry = LLMProviderRegistry.GetEntry(provider.Value);
        var cfg = entry?.CreateConfig(creds);
        if (cfg == null) return null;

        var toLLM = cfg.GetType().GetMethod("ToLLMConfig");
        return toLLM?.Invoke(cfg, null) as LLMConfig;
    }

    private ILLMClient CreateCurrentLLMClient()
    {
        var providerRaw = CredentialService.SelectedLLMProvider;
        var provider = LLMProviderExtensions.FromRawValue(providerRaw) ?? LLMProvider.Doubao;
        return LLMProviderRegistry.CreateClient(provider) ?? new OpenAIChatClient();
    }

    public void Dispose()
    {
        _audioEngine.Dispose();
        _asrClient?.Dispose();
        _eventCts?.Dispose();
        _speculativeDebounceCts?.Dispose();
    }
}
