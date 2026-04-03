using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class FileExplorerControl : BaseStyledControl
{
    private TreeView _treeView = null!;
    private string? _currentPath;
    private FileSystemWatcher? _watcher;
    private System.Windows.Forms.Timer _debounceTimer;
    
    public event Action<string>? OnFileSelected;
    public event Action<string>? OnFolderOpened;

    public FileExplorerControl()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(10); // Restore workspace/explorer margins
        _debounceTimer = new System.Windows.Forms.Timer { Interval = 500 }; // 500ms debounce
        _debounceTimer.Tick += (s, e) => {
            _debounceTimer.Stop();
            RefreshTree();
        };
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = ThemeManager.Sidebar
        };

        var openBtn = new ModernButton
        {
            Text = "📁 Open Folder",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48),
            Font = new Font("Segoe UI", 9f)
        };
        openBtn.Click += (s, e) => OpenFolder();
        header.Controls.Add(openBtn);

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain,
            LineColor = ThemeManager.Border,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f),
            ShowLines = true,
            ItemHeight = 25
        };
        _treeView.NodeMouseDoubleClick += (s, e) => {
            if (e.Node.Tag is string path && File.Exists(path))
                OnFileSelected?.Invoke(path);
        };

        Controls.Add(_treeView);
        Controls.Add(header);
    }

    public void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        _currentPath = path;
        
        InitializeWatcher(path);
        RefreshTree();
    }

    private void InitializeWatcher(string path)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemChanged;
        _watcher.Changed += OnFileSystemChanged;

        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // Don't trigger for ignored files/folders
        if (IsIgnored(e.Name ?? "")) return;

        if (InvokeRequired)
        {
            Invoke(new Action(() => StartDebounce()));
        }
        else
        {
            StartDebounce();
        }
    }

    private void StartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void RefreshTree()
    {
        if (string.IsNullOrEmpty(_currentPath) || !Directory.Exists(_currentPath)) return;

        _treeView.BeginUpdate();
        var expandedPaths = GetExpandedPaths(_treeView.Nodes);
        _treeView.Nodes.Clear();
        
        var rootNode = CreateDirectoryNode(new DirectoryInfo(_currentPath));
        _treeView.Nodes.Add(rootNode);
        
        RestoreExpandedPaths(_treeView.Nodes, expandedPaths);
        rootNode.Expand(); // Ensure root is always expanded
        _treeView.EndUpdate();
    }

    private List<string> GetExpandedPaths(TreeNodeCollection nodes)
    {
        var paths = new List<string>();
        foreach (TreeNode node in nodes)
        {
            if (node.IsExpanded && node.Tag is string path)
            {
                paths.Add(path);
                paths.AddRange(GetExpandedPaths(node.Nodes));
            }
        }
        return paths;
    }

    private void RestoreExpandedPaths(TreeNodeCollection nodes, List<string> paths)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string path && paths.Contains(path))
            {
                node.Expand();
                RestoreExpandedPaths(node.Nodes, paths);
            }
        }
    }

    private TreeNode CreateDirectoryNode(DirectoryInfo directoryInfo)
    {
        var directoryNode = new TreeNode($"📁 {directoryInfo.Name}") { Tag = directoryInfo.FullName };
        
        try
        {
            foreach (var directory in directoryInfo.GetDirectories()
                .Where(d => !IsIgnored(d.Name))
                .OrderBy(d => d.Name))
            {
                directoryNode.Nodes.Add(CreateDirectoryNode(directory));
            }

            foreach (var file in directoryInfo.GetFiles().OrderBy(f => f.Name))
            {
                var fileNode = new TreeNode($"📄 {file.Name}") { Tag = file.FullName };
                directoryNode.Nodes.Add(fileNode);
            }
        }
        catch { /* System files / access denied */ }

        return directoryNode;
    }

    private bool IsIgnored(string name)
    {
        string[] ignored = { ".git", "bin", "obj", ".ollama", ".vs", ".idea", "node_modules" };
        return ignored.Any(i => name.ToLower().Contains(i));
    }

    private void OpenFolder()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadDirectory(dialog.SelectedPath);
            OnFolderOpened?.Invoke(dialog.SelectedPath);
        }
    }
}
