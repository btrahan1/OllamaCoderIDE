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
        if (e.Index < 0 || e.Index >= _tabControl.TabCount) return;

        var tabPage = _tabControl.TabPages[e.Index];
        var tabRect = _tabControl.GetTabRect(e.Index);
        var entry = _openFiles.Values.FirstOrDefault(x => x.Tab == tabPage);
        bool isSelected = e.State.HasFlag(DrawItemState.Selected);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 1. Background with subtle Active Glow
        using (var brush = new SolidBrush(isSelected ? ThemeManager.Sidebar : ThemeManager.Background))
        {
            e.Graphics.FillRectangle(brush, tabRect);
        }

        // 2. Active Tab Accent (Bottom Blue Bar)
        if (isSelected)
        {
            using (var accentBrush = new SolidBrush(ThemeManager.Primary))
            {
                // Draw a 2px bar at the very bottom of the tab
                e.Graphics.FillRectangle(accentBrush, tabRect.X + 2, tabRect.Bottom - 3, tabRect.Width - 4, 3);
            }
        }

        // 3. Text
        string text = tabPage.Text;
        TextRenderer.DrawText(e.Graphics, text, ThemeManager.TextFont, new Rectangle(tabRect.X + 5, tabRect.Y, tabRect.Width - 50, tabRect.Height), 
            isSelected ? ThemeManager.TextMain : ThemeManager.TextSecondary, 
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // 4. Context Brain Indicator (Refined Circle)
        var brainRect = new Rectangle(tabRect.Right - 44, tabRect.Y + (tabRect.Height - 14) / 2, 12, 12);
        if (entry.InContext)
        {
            // Successful context indicator: Primary color with a subtle outer ring
            using (var p = new Pen(ThemeManager.Primary, 1.5f))
                e.Graphics.DrawEllipse(p, brainRect);
            using (var b = new SolidBrush(ThemeManager.Primary))
            {
                var inner = brainRect;
                inner.Inflate(-2, -2);
                e.Graphics.FillEllipse(b, inner);
            }
        }
        else
        {
            // Silent context indicator: Subtle border
            using (var p = new Pen(ThemeManager.Border, 1.5f))
                e.Graphics.DrawEllipse(p, brainRect);
        }

        // 5. Close 'X' button (Simplified and Sharp)
        var closeRect = new Rectangle(tabRect.Right - 22, tabRect.Y + (tabRect.Height - 16) / 2, 16, 16);
        using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
        {
            TextRenderer.DrawText(e.Graphics, "✕", f, closeRect, Color.FromArgb(100, 100, 100), 
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
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
