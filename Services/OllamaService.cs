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

public class OllamaService : ILLMService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434/api";
    private readonly List<ChatMessage> _history = new();
    private readonly ProjectMapService _projectMapService = new();
    
    private string _currentProjectMap = "";
    public string? WorkingDirectory { get; private set; }
    public string? ActiveFileContent { get; set; }
    public string? ActiveFilePath { get; set; }
    public List<FileContext> ContextFiles { get; set; } = new();
    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();
    public event Action<string>? OnPromptSent;

    public OllamaService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public void Reset()
    {
        _history.Clear();
        SaveHistory();
    }

    public void ClearHistory()
    {
        _history.Clear();
    }

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

    public void AddHistory(string role, string content)
    {
        _history.Add(new ChatMessage(role, content));
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

    public async Task<string> ChatAsync(string prompt, AppSettings settings, bool addToHistory = true, bool leanContext = false, System.Threading.CancellationToken ct = default)
    {
        var fullSystemPrompt = settings.AgentSystemPrompt;
        if (!leanContext && !string.IsNullOrEmpty(_currentProjectMap))
            fullSystemPrompt += "\n\n" + _currentProjectMap;
            
        // 1. Add explicitly pinned context files
        if (ContextFiles.Count > 0)
        {
            fullSystemPrompt += "\n\n## PINNED CONTEXT FILES (Reference these for your work):";
            foreach (var file in ContextFiles)
            {
                fullSystemPrompt += $"\n---\nFile: {file.Path}\nContent:\n{file.Content}\n---";
            }
        }

        // 2. Add active file context (if not already pinned)
        if (!string.IsNullOrEmpty(ActiveFilePath) && !ContextFiles.Any(f => f.Path == ActiveFilePath))
            fullSystemPrompt += $"\n\n## ACTIVE FILE CONTEXT:\nPath: {ActiveFilePath}\nContent:\n{ActiveFileContent}";

        var messages = new List<ChatMessage> { new ChatMessage("system", fullSystemPrompt) };
        messages.AddRange(_history);
        messages.Add(new ChatMessage("user", prompt));

        var requestBody = new
        {
            model = settings.SelectedModel,
            messages = messages,
            stream = false,
            options = new { temperature = settings.Temperature, num_ctx = Math.Max(settings.NumCtx, 8192) }
        };

        // Report full prompt log before sending
        try {
            var options = new JsonSerializerOptions { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string log = JsonSerializer.Serialize(requestBody, options);
            OnPromptSent?.Invoke(log);
        } catch { }

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/chat", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonResponse);
        
        string assistantContent = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        
        if (addToHistory)
        {
            _history.Add(new ChatMessage("user", prompt));
            _history.Add(new ChatMessage("assistant", assistantContent));

            if (_history.Count > settings.MaxHistoryMessages)
            {
                int toRemove = _history.Count - settings.MaxHistoryMessages;
                _history.RemoveRange(0, toRemove);
            }
            SaveHistory();
        }

        return assistantContent;
    }
}
