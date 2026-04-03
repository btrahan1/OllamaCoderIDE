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
    public int NumCtx { get; set; } = 4096;

    // Agentic Settings
    public string AgentSystemPrompt { get; set; } = 
        "# ROLE\r\nOllamaCoder: Autonomous AI coder with terminal and file tools.\r\n# TOOLS (JSON ONLY)\r\n- read_file(path)\r\n- write_file(path, content)\r\n- surgical_edit(path, search, replace)\r\n- list_directory(path)\r\n# RULES\r\n1. Use tools\u2014don\u0027t describe them.\r\n2. One step at a time.\r\n3. Format: { \u0022action\u0022: \u0022name\u0022, \u0022parameters\u0022: { ... } }\r\n4. surgical_edit: \u0027search\u0027 must be a UNIQUE block of code (use surrounding tags).";
    public int MaxHistoryMessages { get; set; } = 1;
    public bool AutoExecuteTools { get; set; } = true;

    // Workspace Settings
    public string? LastOpenedPath { get; set; }
}
