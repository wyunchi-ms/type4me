using System.Text.Json;
using System.Text.RegularExpressions;

namespace Type4Me.Services;

/// <summary>
/// Snippet storage — trigger→replacement pairs.
/// Supports flexible regex matching with CJK/ASCII boundary handling.
/// </summary>
public static class SnippetStorage
{
    private const string Key = "snippets";

    public record Snippet(string Trigger, string Value);

    public static Snippet[] Load()
    {
        var raw = SettingsService.Get(Key);
        if (string.IsNullOrEmpty(raw)) return [];

        try
        {
            var arr = JsonSerializer.Deserialize<string[][]>(raw);
            if (arr == null) return [];
            return arr
                .Where(pair => pair.Length >= 2)
                .Select(pair => new Snippet(pair[0], pair[1]))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public static void Save(Snippet[] snippets)
    {
        var arr = snippets.Select(s => new[] { s.Trigger, s.Value }).ToArray();
        SettingsService.Set(Key, JsonSerializer.Serialize(arr));
    }

    /// <summary>
    /// Apply all snippet replacements to the given text.
    /// Handles CJK/ASCII boundary whitespace gracefully.
    /// </summary>
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var snippet in Load())
        {
            if (string.IsNullOrEmpty(snippet.Trigger)) continue;

            // Build flexible regex: optional whitespace around trigger,
            // accounting for CJK/ASCII boundaries
            var escaped = Regex.Escape(snippet.Trigger);
            var pattern = $@"\s*{escaped}\s*";

            text = Regex.Replace(text, pattern, snippet.Value, RegexOptions.IgnoreCase);
        }

        return text;
    }
}
