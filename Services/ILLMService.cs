using System.Collections.Generic;
using System.Threading.Tasks;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public record ChatMessage(string role, string content);

public interface ILLMService
{
    string? WorkingDirectory { get; }
    string? ActiveFileContent { get; set; }
    string? ActiveFilePath { get; set; }
    List<FileContext> ContextFiles { get; set; }
    IReadOnlyList<ChatMessage> History { get; }

    event Action<string>? OnPromptSent;
    Task<string> ChatAsync(string prompt, AppSettings settings, bool addToHistory = true, bool leanContext = false, string? systemPromptOverride = null, System.Threading.CancellationToken ct = default);

    void Reset();
    void ClearHistory();
    void SetWorkingDirectory(string path);
    void RefreshProjectMap();
    void LoadHistory();
    void SaveHistory();
    void AddHistory(string role, string content);
    Task<List<string>> GetModelsAsync();
}
