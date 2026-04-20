using System;
using System.IO;
using System.Text;

namespace OllamaCoderIDE.Services;

public class SessionLogService
{
    private string? _logPath;
    private readonly StringBuilder _buffer = new();

    public void StartSession(string workingDir, string provider)
    {
        try
        {
            DateTime now = DateTime.Now;
            string dirName = provider.ToLower() == "gemini" ? ".gemini_coder" : ".ollama";
            string logDir = Path.Combine(workingDir, dirName, "logs");
            
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            
            string fileName = $"session_{now:yyyyMMdd_HHmmss}.log";
            _logPath = Path.Combine(logDir, fileName);
            
            LogRaw($"--- SESSION STARTED AT {now} ---");
            LogRaw($"--- PROVIDER: {provider} ---\n");
        }
        catch { }
    }

    public void LogRequest(string rawJson)
    {
        LogSection("RAW REQUEST TO LLM (PROMPT + CONTEXT)", rawJson);
    }

    public void LogResponse(string rawText)
    {
        LogSection("RAW RESPONSE FROM LLM", rawText);
    }

    public void LogTool(string name, string parameters, string result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tool: {name}");
        sb.AppendLine($"Args: {parameters}");
        sb.AppendLine($"Result: {result}");
        LogSection("TOOL EXECUTION", sb.ToString());
    }

    public void LogDiagnostic(string message)
    {
        LogRaw($"[DIAGNOSTIC {DateTime.Now:HH:mm:ss.fff}] {message}\n");
    }

    private void LogSection(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"   {title}");
        sb.AppendLine("================================================================================");
        sb.AppendLine(content);
        sb.AppendLine("\n");
        LogRaw(sb.ToString());
    }

    private void LogRaw(string text)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            File.AppendAllText(_logPath, text);
        }
        catch { }
    }

    public string? GetLogPath() => _logPath;
}
