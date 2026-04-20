using System;
using System.Collections.Generic;
using System.Linq;

namespace OllamaCoderIDE.Services;

public enum DiffType { Equal, Insert, Delete }

public class DiffLine
{
    public DiffType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
}

public class DiffService
{
    public List<DiffLine> ComputeDiff(string oldText, string newText)
    {
        var oldLines = (oldText ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var newLines = (newText ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var diff = new List<DiffLine>();
        
        // Simple recursive LCS or dynamic programming for diff
        // For efficiency in large files, we'll use a basic greedy line-by-line approach for now
        // This can be upgraded to Myers later if needed.
        
        int oldIdx = 0;
        int newIdx = 0;

        while (oldIdx < oldLines.Length || newIdx < newLines.Length)
        {
            if (oldIdx < oldLines.Length && newIdx < newLines.Length && oldLines[oldIdx] == newLines[newIdx])
            {
                diff.Add(new DiffLine { Type = DiffType.Equal, Text = oldLines[oldIdx], OldLineNumber = oldIdx + 1, NewLineNumber = newIdx + 1 });
                oldIdx++;
                newIdx++;
            }
            else
            {
                // Look ahead to see if oldLines[oldIdx] appears later in newLines
                int lookAheadNew = -1;
                for (int i = newIdx + 1; i < Math.Min(newIdx + 50, newLines.Length); i++)
                {
                    if (oldIdx < oldLines.Length && oldLines[oldIdx] == newLines[i])
                    {
                        lookAheadNew = i;
                        break;
                    }
                }

                if (lookAheadNew != -1)
                {
                    // Everything between newIdx and lookAheadNew is an insertion
                    for (int i = newIdx; i < lookAheadNew; i++)
                    {
                        diff.Add(new DiffLine { Type = DiffType.Insert, Text = newLines[i], NewLineNumber = i + 1 });
                    }
                    newIdx = lookAheadNew;
                }
                else
                {
                    // If it doesn't appear soon, it's either a deletion or a replacement
                    if (oldIdx < oldLines.Length)
                    {
                        diff.Add(new DiffLine { Type = DiffType.Delete, Text = oldLines[oldIdx], OldLineNumber = oldIdx + 1 });
                        oldIdx++;
                    }
                    else if (newIdx < newLines.Length)
                    {
                        diff.Add(new DiffLine { Type = DiffType.Insert, Text = newLines[newIdx], NewLineNumber = newIdx + 1 });
                        newIdx++;
                    }
                }
            }
        }

        return diff;
    }
}
