using System.Text.Json;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public class SettingsService
{
    private readonly string _settingsFilePath = "appsettings.json";
    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch { /* Fallback to defaults */ }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { /* Handle save errors */ }
    }

    public void Update(AppSettings settings)
    {
        Current = settings;
        Save();
    }
}
