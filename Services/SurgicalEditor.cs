using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OllamaCoderIDE.Services;

public class SurgicalEditor
{
    public static string PreviewEdit(string content, string searchContent, string replacementContent)
    {
        // 0. Pre-check: Is the change ALREADY applied?
        if (content.Contains(replacementContent) && !content.Contains(searchContent))
            return content;

        // 1. Try EXACT match first
        int firstIndex = content.IndexOf(searchContent);
        int lastIndex = content.LastIndexOf(searchContent);

        if (firstIndex != -1 && firstIndex == lastIndex)
        {
            return content.Substring(0, firstIndex) + replacementContent + content.Substring(firstIndex + searchContent.Length);
        }

        // 2. Whitespace-Agnostic fallback
        var (cleanFirst, cleanLast, originalStart, originalEnd) = FindFuzzyMatch(content, searchContent);

        if (cleanFirst != -1 && cleanFirst == cleanLast)
        {
            return content.Substring(0, originalStart) + replacementContent + content.Substring(originalEnd);
        }

        return content; // No change if not found or not unique
    }

    public static string ReplaceContent(string filePath, string searchContent, string replacementContent)
    {
        if (!File.Exists(filePath))
            return $"Error: File '{filePath}' does not exist.";

        string content = File.ReadAllText(filePath);

        // 0. Pre-check: Is the change ALREADY applied?
        if (content.Contains(replacementContent) && !content.Contains(searchContent))
            return $"Note: Change already applied to '{filePath}'.";

        // 1. Try EXACT match first
        int firstIndex = content.IndexOf(searchContent);
        int lastIndex = content.LastIndexOf(searchContent);

        if (firstIndex != -1 && firstIndex == lastIndex)
        {
            var newContent = content.Substring(0, firstIndex) + replacementContent + content.Substring(firstIndex + searchContent.Length);
            File.WriteAllText(filePath, newContent);
            return "Success (Exact Match)";
        }

        // 2. Whitespace-Agnostic fallback
        var (cleanFirst, cleanLast, originalStart, originalEnd) = FindFuzzyMatch(content, searchContent);

        if (cleanFirst != -1 && cleanFirst == cleanLast)
        {
            var finalContent = content.Substring(0, originalStart) + replacementContent + content.Substring(originalEnd);
            File.WriteAllText(filePath, finalContent);
            return "Success (Fuzzy Match)";
        }

        if (firstIndex != -1 && firstIndex != lastIndex)
            return "Error: Search content is not unique.";
        
        return "Error: Could not find search content. Try a larger unique block.";
    }

    public static string InsertContent(string filePath, string anchorContent, string newContent, bool insertAfter = true)
    {
        if (!File.Exists(filePath)) return $"Error: File '{filePath}' does not exist.";

        string content = File.ReadAllText(filePath);
        int index = content.IndexOf(anchorContent);

        if (index == -1)
        {
            // Try fuzzy anchor find
            var (_, _, originalStart, originalEnd) = FindFuzzyMatch(content, anchorContent);
            if (originalStart != -1)
                index = insertAfter ? originalEnd : originalStart;
        }
        else
        {
            index = insertAfter ? index + anchorContent.Length : index;
        }

        if (index == -1) return "Error: Could not find anchor content.";

        string finalContent = content.Substring(0, index) + newContent + content.Substring(index);
        File.WriteAllText(filePath, finalContent);
        return $"Success (Inserted {(insertAfter ? "after" : "before")} anchor)";
    }

    private static (int cleanFirst, int cleanLast, int originalStart, int originalEnd) FindFuzzyMatch(string content, string search)
    {
        string Normalize(string str) => string.Concat(str.Where(c => !char.IsWhiteSpace(c))).Replace('\'', '\"');
        
        string searchClean = Normalize(search);
        if (string.IsNullOrEmpty(searchClean)) return (-1, -1, -1, -1);

        var contentMap = new List<int>();
        var contentCleanSb = new StringBuilder();
        for (int i = 0; i < content.Length; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                contentCleanSb.Append(content[i]);
                contentMap.Add(i);
            }
        }
        string contentClean = contentCleanSb.ToString().Replace('\'', '\"');

        int cleanFirst = contentClean.IndexOf(searchClean);
        int cleanLast = contentClean.LastIndexOf(searchClean);

        if (cleanFirst != -1 && cleanFirst == cleanLast)
        {
            int start = contentMap[cleanFirst];
            int end = contentMap[cleanFirst + searchClean.Length - 1] + 1;
            return (cleanFirst, cleanLast, start, end);
        }

        return (cleanFirst, cleanLast, -1, -1);
    }
}
