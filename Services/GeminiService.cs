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

public class GeminiService : ILLMService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/";
    private readonly List<ChatMessage> _history = new();
    private readonly ProjectMapService _projectMapService = new();
    
    private string _currentProjectMap = "";
    public string? WorkingDirectory { get; private set; }
    public string? ActiveFileContent { get; set; }
    public string? ActiveFilePath { get; set; }
    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();
    public event Action<string>? OnPromptSent;

    public GeminiService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
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
        var dir = Path.Combine(WorkingDirectory, ".gemini_coder");
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
        catch { }
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
        catch { }
    }

    public void AddHistory(string role, string content)
    {
        _history.Add(new ChatMessage(role, content));
    }

    public async Task<List<string>> GetModelsAsync()
    {
        await Task.Yield();
        // Simple list for now as Gemini models are well-known
        return new List<string> { 
            "models/gemini-2.5-flash",
            "models/gemini-2.0-flash",
            "models/gemini-1.5-flash", 
            "models/gemini-1.5-pro" 
        };
    }

    public async Task<string> ChatAsync(string prompt, AppSettings settings, bool addToHistory = true, bool leanContext = false, System.Threading.CancellationToken ct = default)
    {
        var fullSystemPrompt = settings.AgentSystemPrompt;
        if (!leanContext && !string.IsNullOrEmpty(_currentProjectMap))
            fullSystemPrompt += "\n\n" + _currentProjectMap;
            
        if (!string.IsNullOrEmpty(ActiveFilePath))
            fullSystemPrompt += $"\n\n## ACTIVE FILE CONTEXT:\nPath: {ActiveFilePath}\nContent:\n{ActiveFileContent}";

        var messages = new List<object> { new { role = "system", content = fullSystemPrompt } };
        foreach (var msg in _history)
        {
            messages.Add(new { role = msg.role, content = msg.content });
        }

        // Add the current prompt (can be from user or tool results)
        messages.Add(new { role = "user", content = prompt });

        var requestBody = new
        {
            model = settings.GeminiModel,
            messages = messages,
            temperature = settings.Temperature,
            stream = false
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.GeminiApiKey);
        request.Content = JsonContent.Create(requestBody);

        // Report full prompt log before sending
        try {
            var options = new JsonSerializerOptions { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string log = JsonSerializer.Serialize(requestBody, options);
            OnPromptSent?.Invoke(log);
        } catch { }

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonResponse);
        
        string assistantContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
        
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
