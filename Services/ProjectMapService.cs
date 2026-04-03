using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OllamaCoderIDE.Services;

public class ProjectMapService
{
    private class MapNode
    {
        public string Name { get; }
        public Dictionary<string, MapNode> Children { get; } = new();

        public MapNode(string name)
        {
            Name = name;
        }
    }

    public string BuildMap(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            return "No workspace loaded.";

        try
        {
            var allFiles = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                .Where(f => !IsIgnored(f, rootPath))
                .Select(f => Path.GetRelativePath(rootPath, f))
                .ToList();

            var root = new MapNode("");
            foreach (var file in allFiles)
            {
                var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var current = root;
                foreach (var part in parts)
                {
                    if (!current.Children.ContainsKey(part))
                    {
                        current.Children[part] = new MapNode(part);
                    }
                    current = current.Children[part];
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("WORKSPACE PROJECT MAP:");
            RenderTree(root, "", true, sb);
            
            // Add important file summaries
            var readmePath = Path.Combine(rootPath, "README.md");
            if (File.Exists(readmePath))
            {
                sb.AppendLine("\nREADME.md SUMMARY:");
                sb.AppendLine(string.Join("\n", File.ReadLines(readmePath).Take(10)));
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error building project map: {ex.Message}";
        }
    }

    private void RenderTree(MapNode node, string indent, bool isLast, StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(node.Name))
        {
            sb.Append(indent);
            sb.Append(isLast ? "└── " : "├── ");
            sb.AppendLine(node.Name);
            indent += isLast ? "    " : "│   ";
        }

        var children = node.Children.Values.ToList();
        for (int i = 0; i < children.Count; i++)
        {
            RenderTree(children[i], indent, i == children.Count - 1, sb);
        }
    }

    private bool IsIgnored(string fullPath, string rootPath)
    {
        var relative = Path.GetRelativePath(rootPath, fullPath).ToLower();
        
        // Define directory segments to ignore completely
        string[] ignoredSegments = { 
            ".git", "bin", "obj", ".ollama", ".vs", "node_modules", 
            ".idea", "dist", "publish", ".gemini_coder"
        };

        // Aggressively ignore library folders (Bootstrap, etc) which are 99% trash for an AI
        if (relative.Contains("wwwroot" + Path.DirectorySeparatorChar + "lib") || 
            relative.Contains("wwwroot/lib")) 
            return true;

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => ignoredSegments.Contains(p));
    }
}
