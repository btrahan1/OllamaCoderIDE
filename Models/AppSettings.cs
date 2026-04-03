namespace OllamaCoderIDE.Models;

public class AppSettings
{
    public string SelectedModel { get; set; } = "qwen2.5-coder:7b";
    public double Temperature { get; set; } = 0.3;
    public int TopK { get; set; } = 40;
    public double TopP { get; set; } = 1.0;
    public int NumCtx { get; set; } = 4096;

    // Agentic Settings
    public string AgentSystemPrompt { get; set; } = "You are a pragmatic senior software engineer and assistant. Use tools when needed to discover, read, and edit files in the workspace.";
    public int MaxHistoryMessages { get; set; } = 10;
    public bool AutoExecuteTools { get; set; } = false;
}
