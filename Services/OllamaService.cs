using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434/api";

    public OllamaService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Increased timeout for long generations
    }

    public async Task<List<string>> GetModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/tags");
            if (!response.IsSuccessStatusCode) return new List<string> { "qwen3:8b" };

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
            return models.Count > 0 ? models : new List<string> { "qwen3:8b" };
        }
        catch
        {
            return new List<string> { "qwen3:8b" };
        }
    }

    public async Task<string> GenerateAsync(string prompt, AppSettings settings)
    {
        var requestBody = new
        {
            model = settings.SelectedModel,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = settings.Temperature,
                top_k = settings.TopK,
                top_p = settings.TopP,
                num_ctx = settings.NumCtx
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }
}
