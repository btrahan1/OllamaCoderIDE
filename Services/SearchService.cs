using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Services;

public class SearchService
{
    private readonly string[] _ignoredExtensions = { ".dll", ".exe", ".pdb", ".png", ".jpg", ".zip", ".bin", ".obj" };
    private readonly string[] _ignoredDirs = { ".git", "bin", "obj", ".vs", "node_modules", ".gemini_coder", ".ollama", "lib", "dist", "wwwroot/lib" };
    private readonly string[] _priorityExtensions = { ".razor", ".cs", ".css", ".html" };

    public List<FileContext> SearchContext(string rootPath, string query, int topK = 5)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return new List<FileContext>();

        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 3)
                            .Select(w => w.ToLower())
                            .ToList();

        if (keywords.Count == 0) return new List<FileContext>();

        var results = new List<(string Path, int Score)>();

        var files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                             .Where(f => !IsIgnored(f));

        foreach (var file in files)
        {
            try
            {
                // Simple scoring based on keyword frequency in filename and content
                int score = 0;
                string fileName = Path.GetFileName(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                // Priority boost for source files
                if (_priorityExtensions.Contains(ext)) score += 5;
                
                foreach (var kw in keywords)
                {
                    if (fileName.Contains(kw)) score += 10;
                }

                // Read first 10k chars for speed
                using var reader = new StreamReader(file);
                char[] buffer = new char[10000];
                int read = reader.Read(buffer, 0, 10000);
                string content = new string(buffer, 0, read).ToLower();

                foreach (var kw in keywords)
                {
                    int index = 0;
                    while ((index = content.IndexOf(kw, index)) != -1)
                    {
                        score++;
                        index += kw.Length;
                    }
                }

                if (score > 0) results.Add((file, score));
            }
            catch { /* Skip unreadable files */ }
        }

        return results.OrderByDescending(r => r.Score)
                      .Take(topK)
                      .Select(r => new FileContext 
                      { 
                          Path = r.Path, 
                          FileName = Path.GetFileName(r.Path),
                          Content = File.ReadAllText(r.Path)
                      })
                      .ToList();
    }

    private bool IsIgnored(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        if (_ignoredExtensions.Contains(ext)) return true;

        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => _ignoredDirs.Contains(p.ToLower()))) return true;

        // Extra check for wwwroot/lib
        if (path.Contains("wwwroot" + Path.DirectorySeparatorChar + "lib") || 
            path.Contains("wwwroot/lib")) return true;

        return false;
    }
}
