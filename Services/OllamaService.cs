using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public record ChatMessage(string role, string content);

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434/api";
    private readonly List<ChatMessage> _history = new();
    private readonly ProjectMapService _projectMapService = new();
    
    private string _currentProjectMap = "";
    public string? WorkingDirectory { get; private set; }
    public string? ActiveFileContent { get; set; }
    public string? ActiveFilePath { get; set; }
    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

    public OllamaService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public void Reset() => _history.Clear();

    public void SetWorkingDirectory(string path)
    {
        WorkingDirectory = path;
        RefreshProjectMap();
        LoadHistory();
    }

    public void RefreshProjectMap()
    {
        if (!string.IsNullOrEmpty(WorkingDirectory))
        {
            _currentProjectMap = _projectMapService.BuildMap(WorkingDirectory);
        }
    }

    private string GetHistoryPath()
    {
        if (string.IsNullOrEmpty(WorkingDirectory)) return string.Empty;
        var dir = Path.Combine(WorkingDirectory, ".ollama");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, "history.json");
    }

    public void LoadHistory()
    {
        _history.Clear();
        var path = GetHistoryPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<ChatMessage>>(json);
            if (items != null) _history.AddRange(items);
        }
        catch { /* Fallback to empty history */ }
    }

    public void SaveHistory()
    {
        var path = GetHistoryPath();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { /* Ignore save errors */ }
    }

    public async Task<List<string>> GetModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/tags");
            if (!response.IsSuccessStatusCode) return new List<string> { "qwen2.5-coder:7b" };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var models = new List<string>();
            
            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    models.Add(model.GetProperty("name").GetString() ?? "");
                }
            }
            return models.Count > 0 ? models : new List<string> { "qwen2.5-coder:7b" };
        }
        catch { return new List<string> { "qwen2.5-coder:7b" }; }
    }

    public async Task<string> ChatAsync(string prompt, AppSettings settings)
    {
        _history.Add(new ChatMessage("user", prompt));

        if (_history.Count > settings.MaxHistoryMessages)
        {
            int toRemove = _history.Count - settings.MaxHistoryMessages;
            _history.RemoveRange(0, toRemove);
        }

        var fullSystemPrompt = settings.AgentSystemPrompt;
        if (!string.IsNullOrEmpty(_currentProjectMap))
            fullSystemPrompt += "\n\n" + _currentProjectMap;
        if (!string.IsNullOrEmpty(ActiveFilePath))
            fullSystemPrompt += $"\n\n## ACTIVE FILE CONTEXT:\nPath: {ActiveFilePath}\nContent:\n{ActiveFileContent}";

        var messages = new List<ChatMessage> { new ChatMessage("system", fullSystemPrompt) };
        messages.AddRange(_history);

        var requestBody = new
        {
            model = settings.SelectedModel,
            messages = messages,
            stream = false,
            options = new { temperature = settings.Temperature, num_ctx = Math.Max(settings.NumCtx, 8192) }
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/chat", requestBody);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        
        string assistantContent = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        
        _history.Add(new ChatMessage("assistant", assistantContent));
        SaveHistory();

        return assistantContent;
    }
}
