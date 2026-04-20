using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                            .Where(w => w.Length > 2)
                            .Select(w => w.ToLower())
                            .ToList();

        if (keywords.Count == 0) return new List<FileContext>();

        var results = new List<(string Path, double Score)>();
        var files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                             .Where(f => !IsIgnored(f));

        foreach (var file in files)
        {
            try
            {
                double score = 0;
                string fileName = Path.GetFileName(file).ToLower();
                string ext = Path.GetExtension(file).ToLower();

                // 1. Filename Scoring (Highest weight)
                foreach (var kw in keywords)
                {
                    if (fileName == kw) score += 50;
                    else if (fileName.Contains(kw)) score += 20;
                }

                if (score == 0 && !_priorityExtensions.Contains(ext)) continue;

                // 2. Content Scoring (Term Frequency)
                // Read first 50KB for better context/speed balance
                using var reader = new StreamReader(file);
                char[] buffer = new char[50000]; 
                int read = reader.Read(buffer, 0, 50000);
                string content = new string(buffer, 0, read).ToLower();

                foreach (var kw in keywords)
                {
                    int index = 0;
                    int matches = 0;
                    while ((index = content.IndexOf(kw, index)) != -1)
                    {
                        matches++;
                        index += kw.Length;
                        if (matches > 100) break; // Cap individual term frequency
                    }
                    
                    if (matches > 0)
                    {
                        // Logarithmic scale for term frequency to avoid saturation by one file
                        score += Math.Log10(matches + 1) * 10;
                    }
                }

                // 3. Priority Boost
                if (_priorityExtensions.Contains(ext) && score > 0) score *= 1.2;

                if (score > 0) results.Add((file, score));
            }
            catch { }
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

    public string GrepSearch(string rootPath, string pattern, bool isRegex = false)
    {
        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return "Error: No workspace.";
        
        var sb = new StringBuilder();
        var files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                             .Where(f => !IsIgnored(f))
                             .Take(1000); // Sanity limit on file count for grep

        int matchCount = 0;
        Regex? regex = null;
        if (isRegex) 
        {
            try { regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
            catch (Exception ex) { return $"Error: Invalid Regex - {ex.Message}"; }
        }

        foreach (var file in files)
        {
            try
            {
                var lines = File.ReadLines(file);
                int lineNum = 0;
                foreach (var line in lines)
                {
                    lineNum++;
                    bool isMatch = isRegex ? regex!.IsMatch(line) : line.Contains(pattern, StringComparison.OrdinalIgnoreCase);
                    
                    if (isMatch)
                    {
                        matchCount++;
                        sb.AppendLine($"{Path.GetRelativePath(rootPath, file)}:{lineNum}: {line.Trim()}");
                        if (matchCount >= 50) 
                        {
                            sb.AppendLine("\n-- TOP 50 MATCHES SHOWN. REFINE SEARCH IF NEEDED. --");
                            return sb.ToString();
                        }
                    }
                }
            }
            catch { }
        }

        return matchCount > 0 ? sb.ToString() : "No matches found.";
    }

    private bool IsIgnored(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        if (_ignoredExtensions.Contains(ext)) return true;

        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => _ignoredDirs.Contains(p.ToLower()))) return true;

        if (path.Contains("wwwroot" + Path.DirectorySeparatorChar + "lib") || 
            path.Contains("wwwroot/lib")) return true;

        return false;
    }
}
