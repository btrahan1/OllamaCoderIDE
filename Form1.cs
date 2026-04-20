using OllamaCoderIDE.Controls;
using OllamaCoderIDE.Services;
using OllamaCoderIDE.Models;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OllamaCoderIDE;

public partial class Form1 : Form
{
    private readonly ILLMService _ollama;
    private readonly ILLMService _gemini;
    private ILLMService _currentService;
    private readonly SettingsService _settings;
    private SidebarControl _sidebar = null!;
    private EditorTabControl _tabEditor = null!;
    private ChatPanelControl _chat = null!;
    private ToolbarControl _toolbar = null!;
    private SettingsControl _settingsControl = null!;
    private FileExplorerControl _explorer = null!;
    private TerminalControl _terminal = null!;
    private PlanControl _planControl = null!;
    private SearchService _searchService = null!;
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
        _gemini = new GeminiService();
        _settings = new SettingsService();
        _searchService = new SearchService();
        
        // Pick initial service
        _currentService = _settings.Current.Provider == LlmProvider.Gemini ? _gemini : _ollama;

        SetupLayout();

        string root = _settings.Current.LastOpenedPath ?? AppContext.BaseDirectory;
        if (Directory.Exists(root)) OpenFolder(root);

        this.Shown += (s, e) => SetSplitterDistance();
        this.FormClosing += (s, e) => _terminal.Dispose();
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
        contentTable.Controls.Add(_sidebar, 0, 0);

        // 5. Workspace Splitters
        _mainSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 2,
            BackColor = ThemeManager.Border,
            Panel1Collapsed = false
        };
        contentTable.Controls.Add(_mainSplitter, 1, 0);

        _rightContentSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 2,
            BackColor = ThemeManager.Border
        };
        _mainSplitter.Panel2.Controls.Add(_rightContentSplitter);

        _editorChatSplitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 2,
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
        _tabEditor = new EditorTabControl { Dock = DockStyle.Fill };
        _mainPanel.Controls.Add(_tabEditor);
        _tabEditor.OnContextChanged += () => SyncContextToServices();
        _tabEditor.OnFileSelected += (path) => {
            _ollama.ActiveFilePath = path;
            _gemini.ActiveFilePath = path;
        };

        _planControl = new PlanControl { Dock = DockStyle.Fill, Visible = false };
        _mainPanel.Controls.Add(_planControl);

        _settingsControl = new SettingsControl(_settings, _ollama, _gemini) { Dock = DockStyle.Fill, Visible = false };
        _mainPanel.Controls.Add(_settingsControl);

        _editorChatSplitter.Panel1.Controls.Add(_mainPanel);

        // Chat Panel - initially using current service
        _chat = new ChatPanelControl(_currentService, _settings, _terminal, _searchService, _planControl) { Dock = DockStyle.Fill };
        _chat.OnApplyCodeRequested += (code) => {
            string? currentPath = _tabEditor.CurrentFilePath;
            if (!string.IsNullOrEmpty(currentPath))
            {
                try {
                    File.WriteAllText(currentPath, code);
                    HandleFileChange(currentPath);
                } catch { /* Handle lock or write error */ }
            }
        };
        _chat.OnFileModified += (path) => HandleFileChange(path);

        _editorChatSplitter.Panel2.Controls.Add(_chat);
        
        // Orchestrator: Execute a single step from the plan
        _planControl.OnExecuteTask += async (item) => {
            string root = _settings.Current.LastOpenedPath ?? AppContext.BaseDirectory;
            // 1. Automatic RAG: Search for relevant context for this step
            var ragFiles = _searchService.SearchContext(root, item.Title);
            _currentService.ContextFiles = ragFiles; // Load RAG results into AI context
            
            // 2. Switch to Chat to show logic
            OnSidebarViewChanged(SidebarView.AIChat);
            
            // 3. Command the AI to perform ONLY this step
            string taskPrompt = $"[STEP REFINEMENT: {item.Title}]\n[CONTEXT: {item.Description}]\n" + 
                                "Perform ONLY the actions described in this step. Use the provided context files " +
                                "and the most appropriate tool (write_file for new files, surgical_edit for edits) " +
                                "to implement the change.";
            await _chat.ProcessChatAsync(taskPrompt);
            _planControl.MarkItemCompleted(item);
        };

        // Logic to swap services when settings change
        _sidebar.OnViewChanged += (view) => {
            if (view != SidebarView.Settings && _settingsControl.Visible)
            {
                // Leaving settings - check for provider swap
                var newService = _settings.Current.Provider == LlmProvider.Gemini ? _gemini : _ollama;
                if (newService != _currentService)
                {
                    var oldService = _currentService;
                    _currentService = newService;
                    // Transfer state to new service
                    _currentService.ActiveFilePath = _tabEditor.CurrentFilePath;
                    _currentService.ActiveFileContent = _tabEditor.CurrentTextContent;
                    _currentService.ContextFiles = oldService.ContextFiles;
                    _currentService.ClearHistory(); // Avoid mixing history from different providers
                    _currentService.LoadHistory();

                    if (!string.IsNullOrEmpty(_settings.Current.LastOpenedPath))
                        _currentService.SetWorkingDirectory(_settings.Current.LastOpenedPath);
                    
                    UpdateChatService();
                }
            }
            OnSidebarViewChanged(view);
        };

        // Wire Toolbar to Chat
        _toolbar.OnActionRequested += (action) => _chat.PerformAction(action);
        _toolbar.OnProjectTypeChanged += (type) => {
            _settings.Current.ProjectType = type;
            _settings.Save();
            SyncProjectModeToServices();
        };
        _toolbar.SetProjectTypes(_settings.GetProjectTypes(), _settings.Current.ProjectType);
        
        UpdateToolbarStatus();

    }

    private void HandleFileChange(string path)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => HandleFileChange(path)));
            return;
        }

        // 1. Refresh Explorer
        _explorer.RefreshTree();

        // 2. Sync AI Context
        _ollama.ActiveFilePath = path;
        _gemini.ActiveFilePath = path;
        _ollama.RefreshProjectMap();
        _gemini.RefreshProjectMap();

        // 3. Refresh Editor Tab if this file is open
        try {
            string content = File.ReadAllText(path);
            _tabEditor.RefreshFile(path, content);
            
            // Sync current active tab if it matches
            if (path.Equals(_tabEditor.CurrentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _ollama.ActiveFileContent = content;
                _gemini.ActiveFileContent = content;
            }
        } catch { }
    }

    private void UpdateToolbarStatus()
    {
        var s = _settings.Current;
        string model = s.Provider == LlmProvider.Gemini ? s.GeminiModel : s.SelectedModel;
        string provider = s.Provider.ToString().ToUpper();
        Color color = s.Provider == LlmProvider.Gemini ? Color.FromArgb(0, 150, 255) : ThemeManager.Primary;
        
        _toolbar.SetModelStatus($"[{provider}] {model}", color);
    }

    private void UpdateChatService()
    {
        // Re-inject the service into existing controls
        // To avoid deep refactors, I'll add a method to ChatPanelControl
        _chat.UpdateService(_currentService);
        SyncProjectModeToServices();
        UpdateToolbarStatus();
    }

    private void SyncProjectModeToServices()
    {
        // Force refresh of the system prompt in services
        // We'll update the ChatPanelControl to use the new composed prompt
        _chat.RefreshSystemPrompt();
    }


    private void SyncContextToServices()
    {
        var contextFiles = _tabEditor.GetContextFiles()
            .Select(f => new FileContext 
            { 
                Path = f.Path, 
                FileName = Path.GetFileName(f.Path), 
                Content = f.Content 
            }).ToList();

        _ollama.ContextFiles = contextFiles;
        _gemini.ContextFiles = contextFiles;
    }

    private void OpenFolder(string path)
    {
        _explorer.LoadDirectory(path);
        _ollama.SetWorkingDirectory(path);
        _gemini.SetWorkingDirectory(path);
        _terminal.SetWorkingDirectory(path);
        _settings.Current.LastOpenedPath = path;
        _settings.Save();
        _chat.ResetUI(); 
    }

    private void OnFileSelected(string path)
    {
        try {
            string content = File.ReadAllText(path);
            _tabEditor.OpenFile(path, content);

            // Sync with ALL AI contexts (Active File)
            _ollama.ActiveFilePath = path;
            _ollama.ActiveFileContent = content;
            _gemini.ActiveFilePath = path;
            _gemini.ActiveFileContent = content;
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
                _tabEditor.Visible = false;
                _planControl.Visible = false;
                _settingsControl.Visible = true;
                _settingsControl.BringToFront();
                break;
            case SidebarView.Plan:
                _settingsControl.Visible = false;
                _tabEditor.Visible = false;
                _planControl.Visible = true;
                _planControl.BringToFront();
                break;
            case SidebarView.Explorer:
                _mainSplitter.Panel1Collapsed = !_mainSplitter.Panel1Collapsed;
                break;
            default:
                _settingsControl.Visible = false;
                _planControl.Visible = false;
                _tabEditor.Visible = true;
                _tabEditor.BringToFront();
                break;
        }
    }
}
