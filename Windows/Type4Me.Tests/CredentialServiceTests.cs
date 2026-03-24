using Type4Me.Services;
using Xunit;

namespace Type4Me.Tests;

/// <summary>
/// Tests for CredentialService (JSON file persistence).
/// Note: These tests use the real %AppData%\Type4Me\credentials.json file.
/// Test keys use a unique prefix to avoid conflicts.
/// </summary>
public class CredentialServiceTests
{
    private readonly string _testKey = $"test_key_{Guid.NewGuid():N}";

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        CredentialService.Save(_testKey, "my-secret-value");
        var loaded = CredentialService.Load(_testKey);

        Assert.Equal("my-secret-value", loaded);

        // Cleanup
        CredentialService.Delete(_testKey);
    }

    [Fact]
    public void Load_NonExistentKey_ReturnsNull()
    {
        var result = CredentialService.Load($"nonexistent_{Guid.NewGuid():N}");
        Assert.Null(result);
    }

    [Fact]
    public void Delete_RemovesKey()
    {
        CredentialService.Save(_testKey, "to-delete");
        Assert.NotNull(CredentialService.Load(_testKey));

        var deleted = CredentialService.Delete(_testKey);
        Assert.True(deleted);
        Assert.Null(CredentialService.Load(_testKey));
    }

    [Fact]
    public void Delete_NonExistent_ReturnsFalse()
    {
        var result = CredentialService.Delete($"nonexistent_{Guid.NewGuid():N}");
        Assert.False(result);
    }

    [Fact]
    public void SaveAndLoadASRCredentials_RoundTrips()
    {
        var provider = $"test_provider_{Guid.NewGuid():N}";
        var creds = new Dictionary<string, string>
        {
            ["apiKey"] = "test-api-key",
            ["secret"] = "test-secret",
        };

        CredentialService.SaveASRCredentials(provider, creds);
        var loaded = CredentialService.LoadASRCredentials(provider);

        Assert.NotNull(loaded);
        Assert.Equal("test-api-key", loaded["apiKey"]);
        Assert.Equal("test-secret", loaded["secret"]);

        // Cleanup — remove the key from credentials file
        CredentialService.Delete($"tf_asr_{provider}");
    }

    [Fact]
    public void LoadASRCredentials_NonExistentProvider_ReturnsNull()
    {
        var result = CredentialService.LoadASRCredentials($"nonexistent_{Guid.NewGuid():N}");
        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoadLLMCredentials_RoundTrips()
    {
        var provider = $"test_llm_{Guid.NewGuid():N}";
        var creds = new Dictionary<string, string>
        {
            ["apiKey"] = "llm-key",
            ["model"] = "gpt-4",
        };

        CredentialService.SaveLLMCredentials(provider, creds);
        var loaded = CredentialService.LoadLLMCredentials(provider);

        Assert.NotNull(loaded);
        Assert.Equal("llm-key", loaded["apiKey"]);
        Assert.Equal("gpt-4", loaded["model"]);

        // Cleanup
        CredentialService.Delete($"tf_llm_{provider}");
    }

    [Fact]
    public void Save_OverwritesExistingValue()
    {
        CredentialService.Save(_testKey, "first");
        CredentialService.Save(_testKey, "second");
        var loaded = CredentialService.Load(_testKey);

        Assert.Equal("second", loaded);

        // Cleanup
        CredentialService.Delete(_testKey);
    }
}
