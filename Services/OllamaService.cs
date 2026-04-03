using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public record ChatMessage(string role, string content);

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434/api";
    private readonly List<ChatMessage> _history = new();

    public OllamaService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    public void Reset() => _history.Clear();

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
        // 1. Add user message to history
        _history.Add(new ChatMessage("user", prompt));

        // 2. Apply sliding window
        if (_history.Count > settings.MaxHistoryMessages)
        {
            int toRemove = _history.Count - settings.MaxHistoryMessages;
            _history.RemoveRange(0, toRemove);
        }

        // 3. Prepare messages (starting with system prompt)
        var messages = new List<ChatMessage> { new ChatMessage("system", settings.AgentSystemPrompt) };
        messages.AddRange(_history);

        // 4. Request body for /api/chat
        var requestBody = new
        {
            model = settings.SelectedModel,
            messages = messages,
            stream = false,
            options = new
            {
                temperature = settings.Temperature,
                top_k = settings.TopK,
                top_p = settings.TopP,
                num_ctx = settings.NumCtx
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/chat", requestBody);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);
        
        string assistantContent = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? "";
        
        // 5. Add assistant response to history
        _history.Add(new ChatMessage("assistant", assistantContent));

        return assistantContent;
    }
}
