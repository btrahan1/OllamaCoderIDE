using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace OllamaCoderIDE.Services;

public class ToolCall
{
    public string Action { get; set; } = "";
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

public class ToolParseResult
{
    public List<ToolCall> Tools { get; set; } = new();
    public List<string> ParseErrors { get; set; } = new();
}

public class ToolParser
{
    public static ToolParseResult Parse(string content)
    {
        var result = new ToolParseResult();
        if (string.IsNullOrWhiteSpace(content)) return result;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        int braceCount = 0;
        int start = -1;
        bool inQuote = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (c == '\"' && (i == 0 || content[i - 1] != '\\'))
            {
                inQuote = !inQuote;
            }

            if (!inQuote)
            {
                if (c == '{')
                {
                    if (braceCount == 0) start = i;
                    braceCount++;
                }
                else if (c == '}')
                {
                    if (braceCount > 0)
                    {
                        braceCount--;
                        if (braceCount == 0 && start != -1)
                        {
                            string json = content.Substring(start, i - start + 1);
                            if (json.Contains("\"action\"", StringComparison.OrdinalIgnoreCase))
                            {
                                ProcessJsonBlock(json, result);
                            }
                            start = -1;
                        }
                    }
                }
            }
        }

        // RECOVERY: If the string ends while we are still inside a potential tool call block
        if (braceCount > 0 && start != -1)
        {
            string truncatedJson = content.Substring(start);
            if (truncatedJson.Contains("\"action\"", StringComparison.OrdinalIgnoreCase))
            {
                ProcessJsonBlock(truncatedJson, result);
            }
        }
        
        sw.Stop();
        if (sw.ElapsedMilliseconds > 100)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF] ToolParser.Parse took {sw.ElapsedMilliseconds}ms for {content.Length} chars.");
        }

        return result;
    }

    private static void ProcessJsonBlock(string json, ToolParseResult result)
    {
        // RECOVERY: Handle truncated and malformed JSON
        string sanitizedJson = SanitizeJson(json);
        try
        {
            var call = JsonSerializer.Deserialize<ToolCall>(sanitizedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (call != null && !string.IsNullOrEmpty(call.Action))
            {
                result.Tools.Add(call);
            }
            else
            {
                result.ParseErrors.Add($"Tool detected but 'action' was missing or null.");
            }
        }
        catch (Exception ex)
        {
            string errMsg = $"JSON Syntax Error: {ex.Message}. Potential malformed JSON detected.";
            result.ParseErrors.Add(errMsg);
            System.Diagnostics.Debug.WriteLine($"{errMsg}\nJSON: {json}");
        }
    }

    private static string SanitizeJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        // 1. Detect and fix truncation (missing closing delimiters)
        json = EnsureClosed(json);

        // 2. Linear Repair: Escape rogue quotes and control chars in values
        return RepairMalformedJson(json);
    }

    private static string EnsureClosed(string json)
    {
        json = json.Trim();

        int openBraces = 0;
        int openBrackets = 0;
        bool inQuote = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\"' && (i == 0 || json[i - 1] != '\\')) inQuote = !inQuote;
            if (!inQuote)
            {
                if (c == '{') openBraces++;
                else if (c == '}') openBraces--;
                else if (c == '[') openBrackets++;
                else if (c == ']') openBrackets--;
            }
        }

        StringBuilder sb = new StringBuilder(json);
        if (inQuote) sb.Append("\"");
        while (openBraces > 0) { sb.Append("}"); openBraces--; }
        while (openBrackets > 0) { sb.Append("]"); openBrackets--; }

        return sb.ToString();
    }

    private static string RepairMalformedJson(string json)
    {
        // This is a linear state-machine to repair internal quotes in string values.
        // It's MUCH safer than Regex for large code blocks.
        var sb = new StringBuilder();
        bool inQuote = false;
        
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            
            // Check for start/end of string values
            if (c == '\"' && (i == 0 || json[i-1] != '\\'))
            {
                // Is this a structural quote (separator) or content quote?
                // We assume structural quotes are followed by : or , or } or ] 
                // OR preceded by { or , or :
                bool isStructural = IsStructuralQuote(json, i);
                
                if (isStructural)
                {
                    inQuote = !inQuote;
                    sb.Append(c);
                }
                else if (inQuote)
                {
                    // Rogue quote inside a string - escape it!
                    sb.Append("\\\"");
                }
                else
                {
                    // Rogue quote outside - treat as start of new string
                    inQuote = true;
                    sb.Append(c);
                }
                continue;
            }

            if (inQuote)
            {
                // Escape raw newlines and tabs inside quotes
                if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else sb.Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsStructuralQuote(string json, int index)
    {
        // A very heuristic approach: Is this quote likely a JSON delimiter?
        // Structural quotes are usually:
        // 1. Preceded by { or , or : (ignoring whitespace)
        // 2. Followed by : or , or } or ] (ignoring whitespace)
        
        // Peek Back
        int prev = index - 1;
        while (prev >= 0 && char.IsWhiteSpace(json[prev])) prev--;
        if (prev >= 0 && (json[prev] == '{' || json[prev] == ',' || json[prev] == ':' || json[prev] == '[')) return true;

        // Peek Forward
        int next = index + 1;
        while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
        if (next < json.Length && (json[next] == ':' || json[next] == ',' || json[next] == '}' || json[next] == ']' || json[next] == '[')) return true;

        // Start/End of block
        if (index == 0 || index == json.Length - 1) return true;

        return false;
    }
}
