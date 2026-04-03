using OllamaCoderIDE.Controls;
using OllamaCoderIDE.Services;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OllamaCoderIDE;

public partial class Form1 : Form
{
    private readonly OllamaService _ollama;
    private readonly SettingsService _settings;
    private SidebarControl _sidebar = null!;
    private EditorControl _editor = null!;
    private ChatPanelControl _chat = null!;
    private ToolbarControl _toolbar = null!;
    private SettingsControl _settingsControl = null!;
    private FileExplorerControl _explorer = null!;
    private TerminalControl _terminal = null!;
    private SplitContainer _mainSplitter = null!;
    private SplitContainer _rightContentSplitter = null!;
    private SplitContainer _editorChatSplitter = null!;
    private Panel _mainPanel = null!;

    public Form1()
    {
        InitializeComponent();

        this.Text = "Ollama Coder IDE";
        this.BackColor = ThemeManager.Background;
        this.Size = new Size(1300, 850);
        this.StartPosition = FormStartPosition.CenterScreen;

        _ollama = new OllamaService();
        _settings = new SettingsService();
        
        SetupLayout();

        string root = _settings.Current.LastOpenedPath ?? AppContext.BaseDirectory;
        if (Directory.Exists(root)) OpenFolder(root);

        this.Shown += (s, e) => SetSplitterDistance();
    }

    private void SetupLayout()
    {
        // 1. Master Table (2 Rows: Toolbar | Everything Else)
        var masterTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = ThemeManager.Background,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        masterTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Toolbar
        masterTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
        Controls.Add(masterTable);

        // 2. Toolbar
        _toolbar = new ToolbarControl { Dock = DockStyle.Fill };
        masterTable.Controls.Add(_toolbar, 0, 0);

        // 3. Content Table (2 Columns: Sidebar | Workspace)
        var contentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        contentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60)); // Sidebar
        contentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Workspace
        masterTable.Controls.Add(contentTable, 0, 1);

        // 4. Sidebar
        _sidebar = new SidebarControl { Dock = DockStyle.Fill };
        _sidebar.OnViewChanged += OnSidebarViewChanged;
        contentTable.Controls.Add(_sidebar, 0, 0);

        // 5. Workspace Splitters
        _mainSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 5,
            BackColor = ThemeManager.Border,
            Panel1Collapsed = false
        };
        contentTable.Controls.Add(_mainSplitter, 1, 0);

        _rightContentSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 5,
            BackColor = ThemeManager.Border
        };
        _mainSplitter.Panel2.Controls.Add(_rightContentSplitter);

        _editorChatSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 5,
            BackColor = ThemeManager.Border
        };
        _rightContentSplitter.Panel1.Controls.Add(_editorChatSplitter);

        // 6. Components
        _explorer = new FileExplorerControl { Dock = DockStyle.Fill };
        _explorer.OnFileSelected += OnFileSelected;
        _explorer.OnFolderOpened += OpenFolder;
        _mainSplitter.Panel1.Controls.Add(_explorer);

        _terminal = new TerminalControl { Dock = DockStyle.Fill };
        _rightContentSplitter.Panel2.Controls.Add(_terminal);

        _mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.Background };
        _editor = new EditorControl { Dock = DockStyle.Fill };
        _mainPanel.Controls.Add(_editor);

        _settingsControl = new SettingsControl(_settings, _ollama) { Dock = DockStyle.Fill, Visible = false };
        _mainPanel.Controls.Add(_settingsControl);

        _editorChatSplitter.Panel1.Controls.Add(_mainPanel);

        _chat = new ChatPanelControl(_ollama, _settings, _terminal) { Dock = DockStyle.Fill };
        _chat.OnApplyCodeRequested += (code) => {
            _editor.TextContent = code;
            if (!string.IsNullOrEmpty(_editor.CurrentFilePath))
            {
                try {
                File.WriteAllText(_editor.CurrentFilePath, code);
                _ollama.ActiveFileContent = code; // Sync back to AI context
                _ollama.RefreshProjectMap(); 
                } catch { /* Handle lock or write error */ }
            }
        };
        _editorChatSplitter.Panel2.Controls.Add(_chat);

        // Wire Toolbar to Chat
        _toolbar.OnActionRequested += (action) => _chat.PerformAction(action);
    }

    private void OpenFolder(string path)
    {
        _explorer.LoadDirectory(path);
        _ollama.SetWorkingDirectory(path);
        _terminal.SetWorkingDirectory(path);
        _settings.Current.LastOpenedPath = path;
        _settings.Save();
        _chat.ResetUI(); 
    }

    private void OnFileSelected(string path)
    {
        try {
            string content = File.ReadAllText(path);
            _editor.TextContent = content;
            _editor.SetLanguage(Path.GetExtension(path));
            _editor.CurrentFilePath = path;

            // Sync with AI context
            _ollama.ActiveFilePath = path;
            _ollama.ActiveFileContent = content;
        } catch (Exception ex) {
            MessageBox.Show($"Error opening file: {ex.Message}");
        }
    }

    private void SetSplitterDistance()
    {
        try
        {
            _mainSplitter.Panel1MinSize = 150;
            _mainSplitter.SplitterDistance = 220;

            _rightContentSplitter.Panel1MinSize = 250;
            _rightContentSplitter.Panel2MinSize = 100;
            _rightContentSplitter.SplitterDistance = _rightContentSplitter.Height - 200;

            _editorChatSplitter.Panel1MinSize = 300;
            _editorChatSplitter.Panel2MinSize = 250;
            int available = _editorChatSplitter.Width;
            if (available <= 0) return;
            int target = (int)(available * 0.65);
            _editorChatSplitter.SplitterDistance = Math.Clamp(target, 300, available - 250);
        }
        catch { }
    }

    private void OnSidebarViewChanged(SidebarView view)
    {
        switch (view)
        {
            case SidebarView.Settings:
                _editor.Visible = false;
                _settingsControl.Visible = true;
                _settingsControl.BringToFront();
                break;
            case SidebarView.Explorer:
                _mainSplitter.Panel1Collapsed = !_mainSplitter.Panel1Collapsed;
                break;
            default:
                _settingsControl.Visible = false;
                _editor.Visible = true;
                _editor.BringToFront();
                break;
        }
    }
}
