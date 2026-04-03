using System;

namespace OllamaCoderIDE.Models;

public class FileContext
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
