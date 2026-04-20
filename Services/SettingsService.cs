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

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        Save();
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

    public string GetFullSystemPrompt()
    {
        var generalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Instructions", "General.md");
        var projectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Instructions", $"{Current.ProjectType}.md");

        var prompt = "";
        if (File.Exists(generalPath))
        {
            prompt = File.ReadAllText(generalPath);
        }

        if (Current.ProjectType != "General" && File.Exists(projectPath))
        {
            prompt += "\n\n" + File.ReadAllText(projectPath);
        }

        if (!string.IsNullOrEmpty(Current.AgentSystemPrompt))
        {
            prompt += "\n\n### USER OVERRIDES\n" + Current.AgentSystemPrompt;
        }

        return prompt;
    }

    public List<string> GetProjectTypes()
    {
        var instructionsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Instructions");
        if (!Directory.Exists(instructionsDir)) return new List<string> { "General" };

        var files = Directory.GetFiles(instructionsDir, "*.md");
        var types = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        
        // Ensure General is first, then rest alphabetical
        return types.OrderBy(t => t == "General" ? 0 : 1).ThenBy(t => t).ToList();
    }
}


