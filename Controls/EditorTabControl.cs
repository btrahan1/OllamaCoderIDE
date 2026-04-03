using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Controls;

public class EditorTabControl : BaseStyledControl
{
    private TabControl _tabControl = null!;
    private Dictionary<string, (TabPage Tab, EditorControl Editor, bool InContext)> _openFiles = new();
    
    public event Action<string>? OnFileSelected;
    public event Action? OnContextChanged;

    public EditorTabControl()
    {
        Dock = DockStyle.Fill;
        InitializeTabs();
    }

    public List<(string Path, string Content)> GetContextFiles()
    {
        return _openFiles.Values
            .Where(x => x.InContext)
            .Select(x => (x.Editor.CurrentFilePath ?? "", x.Editor.TextContent))
            .ToList();
    }

    private void InitializeTabs()
    {
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            Padding = new Point(25, 5) // Extra space for X and Toggle icons
        };

        _tabControl.DrawItem += OnDrawItem;
        _tabControl.MouseDown += OnMouseDown;
        _tabControl.SelectedIndexChanged += (s, e) => {
            if (_tabControl.SelectedTab != null)
            {
                var entry = _openFiles.Values.FirstOrDefault(x => x.Tab == _tabControl.SelectedTab);
                if (entry.Editor != null) OnFileSelected?.Invoke(entry.Editor.CurrentFilePath ?? "");
            }
        };

        Controls.Add(_tabControl);
    }

    public void OpenFile(string path, string content)
    {
        string normalizedPath = Path.GetFullPath(path);
        if (_openFiles.TryGetValue(normalizedPath, out var existing))
        {
            _tabControl.SelectedTab = existing.Tab;
            return;
        }

        var editor = new EditorControl { Dock = DockStyle.Fill };
        editor.TextContent = content;
        editor.CurrentFilePath = normalizedPath;
        editor.SetLanguage(Path.GetExtension(normalizedPath));

        var tabPage = new TabPage(Path.GetFileName(path))
        {
            BackColor = ThemeManager.Background,
            ToolTipText = normalizedPath
        };
        tabPage.Controls.Add(editor);

        _tabControl.TabPages.Add(tabPage);
        _openFiles[normalizedPath] = (tabPage, editor, true); // Default to IN context when opened
        _tabControl.SelectedTab = tabPage;
        OnContextChanged?.Invoke();
    }

    public void RefreshFile(string path, string content)
    {
        string normalizedPath = Path.GetFullPath(path);
        if (_openFiles.TryGetValue(normalizedPath, out var existing))
        {
            existing.Editor.TextContent = content;
        }
    }

    public string? CurrentFilePath => _tabControl.SelectedTab != null ? 
        _openFiles.Values.FirstOrDefault(x => x.Tab == _tabControl.SelectedTab).Editor?.CurrentFilePath : null;

    public string? CurrentTextContent => _tabControl.SelectedTab != null ? 
        _openFiles.Values.FirstOrDefault(x => x.Tab == _tabControl.SelectedTab).Editor?.TextContent : null;

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        var tabPage = _tabControl.TabPages[e.Index];
        var tabRect = _tabControl.GetTabRect(e.Index);
        var entry = _openFiles.Values.FirstOrDefault(x => x.Tab == tabPage);

        // 1. Background
        using (var brush = new SolidBrush(e.State == DrawItemState.Selected ? ThemeManager.Sidebar : ThemeManager.Background))
        {
            e.Graphics.FillRectangle(brush, tabRect);
        }

        // 2. Text
        string text = tabPage.Text;
        TextRenderer.DrawText(e.Graphics, text, ThemeManager.TextFont, tabRect, 
            e.State == DrawItemState.Selected ? ThemeManager.TextMain : ThemeManager.TextSecondary, 
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // 3. Context Brain Icon (🧠 or Lightbulb if brain emoji fails)
        // Draw 🧠 icon on the right
        var brainRect = new Rectangle(tabRect.Right - 42, tabRect.Y + 6, 18, 18);
        using (var brainBrush = new SolidBrush(entry.InContext ? ThemeManager.Success : Color.Gray))
        {
            e.Graphics.FillEllipse(brainBrush, brainRect); // Simulating a simple brain/context indicator
        }

        // 4. Close 'X' button
        var closeRect = new Rectangle(tabRect.Right - 20, tabRect.Y + 6, 16, 16);
        e.Graphics.DrawString("x", new Font("Segoe UI", 9f), Brushes.Gray, closeRect);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        for (int i = 0; i < _tabControl.TabCount; i++)
        {
            var tabRect = _tabControl.GetTabRect(i);
            
            // Close button click
            var closeRect = new Rectangle(tabRect.Right - 20, tabRect.Y + 6, 16, 16);
            if (closeRect.Contains(e.Location))
            {
                CloseTab(i);
                return;
            }

            // Context brain click
            var brainRect = new Rectangle(tabRect.Right - 42, tabRect.Y + 6, 18, 18);
            if (brainRect.Contains(e.Location))
            {
                ToggleContext(i);
                return;
            }
        }
    }

    private void CloseTab(int index)
    {
        var tabPage = _tabControl.TabPages[index];
        var entry = _openFiles.Values.FirstOrDefault(x => x.Tab == tabPage);
        if (entry.Editor != null && entry.Editor.CurrentFilePath != null)
        {
            _openFiles.Remove(entry.Editor.CurrentFilePath);
        }
        _tabControl.TabPages.RemoveAt(index);
        OnContextChanged?.Invoke();
    }

    private void ToggleContext(int index)
    {
        var tabPage = _tabControl.TabPages[index];
        string? path = null;
        foreach(var kv in _openFiles) 
            if(kv.Value.Tab == tabPage) { path = kv.Key; break; }

        if (path != null)
        {
            var val = _openFiles[path];
            _openFiles[path] = (val.Tab, val.Editor, !val.InContext);
            _tabControl.Invalidate();
            OnContextChanged?.Invoke();
        }
    }
}
