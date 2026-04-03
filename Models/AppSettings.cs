namespace OllamaCoderIDE.Models;

public class AppSettings
{
    public string SelectedModel { get; set; } = "qwen2.5-coder:7b";
    public double Temperature { get; set; } = 0.3;
    public int TopK { get; set; } = 40;
    public double TopP { get; set; } = 1.0;
    public int NumCtx { get; set; } = 4096;

    // Agentic Settings
    public string AgentSystemPrompt { get; set; } = 
        "You are OllamaCoder, an autonomous AI coding assistant. You have direct access to the user's local machine via tools.\n\n" +
        "CORE RULES:\n" +
        "1. Use tools to DISCOVER, READ, EDIT, and EXECUTE code. Do not just describe actions—perform them.\n" +
        "2. If a task requires multiple steps, perform them one at a time, waiting for tool results after each step.\n" +
        "3. ALWAYS format tool calls as JSON blocks using the structure: { \"action\": \"name\", \"parameters\": { ... } }.\n" +
        "4. When using run_command, wait for the result to confirm success before continuing.\n" +
        "5. PREFER `dotnet watch` for dev-serving; if a port is in use, use `kill_port` first.\n\n" +
        "AVAILABLE TOOLS:\n" +
        "- read_file(path): Returns file content.\n" +
        "- write_file(path, content): Overwrites a file.\n" +
        "- surgical_edit(path, search, replace): Replaces specific code blocks using fuzzy matching.\n" +
        "- list_directory(path): Lists files in a directory.\n" +
        "- run_command(command): Executes a shell command in the workspace terminal.\n" +
        "- kill_port(port): Forcefully terminates any process listening on the specified network port.\n";
    public int MaxHistoryMessages { get; set; } = 10;
    public bool AutoExecuteTools { get; set; } = false;

    // Workspace Settings
    public string? LastOpenedPath { get; set; }
}
