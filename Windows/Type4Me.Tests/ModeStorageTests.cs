using Type4Me.Models;
using Type4Me.Services;
using Xunit;

namespace Type4Me.Tests;

/// <summary>
/// Tests for ModeStorage (modes.json persistence).
/// </summary>
public class ModeStorageTests
{
    private readonly ModeStorage _storage = new();

    [Fact]
    public void Load_Default_ReturnsDefaults()
    {
        // Load always returns modes (defaults if file doesn't exist or is empty)
        var modes = _storage.Load();

        Assert.NotNull(modes);
        Assert.NotEmpty(modes);

        // Should contain built-in modes
        Assert.Contains(modes, m => m.Id == ProcessingMode.DirectId);
        Assert.Contains(modes, m => m.Id == ProcessingMode.PerformanceId);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var original = _storage.Load();

        // Add a new custom mode
        var customId = Guid.NewGuid();
        var allModes = original.Append(new ProcessingMode
        {
            Id = customId,
            Name = "Test Custom Mode",
            Prompt = "Test prompt for {text}",
            IsBuiltin = false,
        }).ToArray();

        _storage.Save(allModes);

        var loaded = _storage.Load();
        Assert.Contains(loaded, m => m.Id == customId);
        var custom = loaded.First(m => m.Id == customId);
        Assert.Equal("Test Custom Mode", custom.Name);
        Assert.Equal("Test prompt for {text}", custom.Prompt);
        Assert.False(custom.IsBuiltin);

        // Cleanup: save back original
        _storage.Save(original);
    }

    [Fact]
    public void Load_EnsuresBuiltinModesExist()
    {
        // Save modes WITHOUT built-in Direct mode
        var customOnly = new[]
        {
            new ProcessingMode
            {
                Id = Guid.NewGuid(),
                Name = "Custom Only",
                Prompt = "test",
                IsBuiltin = false,
            },
        };

        _storage.Save(customOnly);

        var loaded = _storage.Load();

        // Built-in modes should be re-inserted
        Assert.Contains(loaded, m => m.Id == ProcessingMode.DirectId);
        Assert.Contains(loaded, m => m.Id == ProcessingMode.PerformanceId);
        // Custom mode should still be there
        Assert.Contains(loaded, m => m.Name == "Custom Only");

        // Cleanup: restore defaults
        _storage.Save(ProcessingMode.Defaults);
    }

    [Fact]
    public void ProcessingMode_Direct_IsBuiltinAndDirect()
    {
        var direct = ProcessingMode.Direct;
        Assert.True(direct.IsBuiltin);
        Assert.True(direct.IsDirect); // Empty prompt
        Assert.Equal(ProcessingMode.DirectId, direct.Id);
    }

    [Fact]
    public void ProcessingMode_SmartDirect_IsNotBuiltinButHasPrompt()
    {
        var smart = ProcessingMode.SmartDirect;
        Assert.False(smart.IsBuiltin);
        Assert.False(smart.IsDirect); // Has a prompt
        Assert.True(smart.IsSmartDirect);
    }

    [Fact]
    public void ProcessingMode_Defaults_ContainsExpectedModes()
    {
        var defaults = ProcessingMode.Defaults;
        Assert.True(defaults.Length >= 4); // At least Direct, Performance, plus some custom

        // Direct and Performance must be present
        Assert.Contains(defaults, m => m.Id == ProcessingMode.DirectId);
        Assert.Contains(defaults, m => m.Id == ProcessingMode.PerformanceId);
    }
}
