namespace OllamaCoderIDE.Models;

public enum LlmProvider { Ollama, Gemini }

public class AppSettings
{
    public LlmProvider Provider { get; set; } = LlmProvider.Ollama;
    public string SelectedModel { get; set; } = "qwen2.5-coder:7b";
    
    // Gemini Settings (Defaults from User)
    public string GeminiModel { get; set; } = "models/gemini-2.5-flash";
    public string GeminiApiKey { get; set; } = "YOUR_API_KEY_HERE";

    public double Temperature { get; set; } = 0.3;
    public int TopK { get; set; } = 40;
    public double TopP { get; set; } = 1.0;
    public int NumCtx { get; set; } = 16384;

    // Agentic Settings
    public string ProjectType { get; set; } = "General";
    public string AgentSystemPrompt { get; set; } = ""; // User overrides if provided

    public int MaxHistoryMessages { get; set; } = 1;
    public bool AutoExecuteTools { get; set; } = true;
    public bool FullFileReplacementOnly { get; set; } = false;

    // Workspace Settings
    public string? LastOpenedPath { get; set; }
}
