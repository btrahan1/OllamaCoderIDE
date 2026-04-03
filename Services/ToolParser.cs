using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OllamaCoderIDE.Services;

public class ToolCall
{
    public string Action { get; set; } = "";
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

public class ToolParser
{
    public static List<ToolCall> Parse(string content)
    {
        var result = new List<ToolCall>();
        if (string.IsNullOrWhiteSpace(content)) return result;

        // Try formal JSON parsing if the whole response is JSON
        try
        {
            var call = JsonSerializer.Deserialize<ToolCall>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (call != null && !string.IsNullOrEmpty(call.Action))
            {
                result.Add(call);
                return result;
            }
        }
        catch { /* Not a pure JSON response */ }

        // Fallback: Use Regex to find JSON-like blocks with balanced braces
        // This handles nested objects in "parameters"
        var matches = Regex.Matches(content, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!))\}", RegexOptions.Singleline);
        
        foreach (Match match in matches)
        {
            if (!match.Value.Contains("\"action\"", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var call = JsonSerializer.Deserialize<ToolCall>(match.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (call != null && !string.IsNullOrEmpty(call.Action))
                {
                    result.Add(call);
                }
            }
            catch { /* Skip malformed block */ }
        }

        return result;
    }
}
