using System.IO;
using System.Text.Json;
using Type4Me.Models;
using Type4Me.Services;

namespace Type4Me.Services;

/// <summary>
/// Persists processing modes to %AppData%\Type4Me\modes.json.
/// </summary>
public class ModeStorage
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Type4Me");

    private static readonly string _filePath = Path.Combine(_dir, "modes.json");
    private static readonly object _lock = new();

    static ModeStorage()
    {
        Directory.CreateDirectory(_dir);
    }

    public ProcessingMode[] Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                    return ProcessingMode.Defaults;

                var json = File.ReadAllText(_filePath);
                var modes = JsonSerializer.Deserialize<ProcessingMode[]>(json);
                if (modes == null || modes.Length == 0)
                    return ProcessingMode.Defaults;

                // Ensure built-in modes exist
                return EnsureBuiltins(modes);
            }
            catch
            {
                return ProcessingMode.Defaults;
            }
        }
    }

    public void Save(ProcessingMode[] modes)
    {
        lock (_lock)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(modes, opts);
            File.WriteAllText(_filePath, json);
        }
    }

    private static ProcessingMode[] EnsureBuiltins(ProcessingMode[] modes)
    {
        var list = modes.ToList();
        foreach (var builtin in ProcessingMode.Builtins)
        {
            if (!list.Any(m => m.Id == builtin.Id))
                list.Insert(0, builtin);
        }
        return list.ToArray();
    }
}
