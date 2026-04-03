namespace OllamaCoderIDE.Models;

public class AppSettings
{
    public string SelectedModel { get; set; } = "qwen3:8b";
    public double Temperature { get; set; } = 0.7;
    public int TopK { get; set; } = 40;
    public double TopP { get; set; } = 1.0;
    public int NumCtx { get; set; } = 4096;
}
