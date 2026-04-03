using OllamaCoderIDE.Controls;
using OllamaCoderIDE.Services;

namespace OllamaCoderIDE;

public partial class Form1 : Form
{
    private readonly OllamaService _ollama;
    private readonly SettingsService _settings;
    private SidebarControl _sidebar = null!;
    private EditorControl _editor = null!;
    private ChatPanelControl _chat = null!;
    private SettingsControl _settingsControl = null!;
    private SplitContainer _splitContainer = null!;
    private Panel _mainPanel = null!; // holds editor OR settings

    public Form1()
    {
        InitializeComponent();

        // Size MUST be set before SetupLayout so the SplitContainer has real dimensions
        this.Text = "Ollama Coder IDE";
        this.BackColor = ThemeManager.Background;
        this.Size = new Size(1300, 850);
        this.StartPosition = FormStartPosition.CenterScreen;

        _ollama = new OllamaService();
        _settings = new SettingsService();
        SetupLayout();

        // Shown fires after the form is fully rendered at its real size
        this.Shown += (s, e) => SetSplitterDistance();
    }

    private void SetupLayout()
    {
        // IMPORTANT: In WinForms docking, Add Fill controls BEFORE Left/Right controls
        // SplitContainer fills the rest (Editor | Chat)
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 5,
            BackColor = ThemeManager.Border
        };

        // Left panel of splitter: main content area (editor or settings)
        _mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeManager.Background };

        _editor = new EditorControl();
        _mainPanel.Controls.Add(_editor);

        _settingsControl = new SettingsControl(_settings, _ollama);
        _settingsControl.Visible = false;
        _mainPanel.Controls.Add(_settingsControl);

        _splitContainer.Panel1.Controls.Add(_mainPanel);

        // Right panel of splitter: chat
        _chat = new ChatPanelControl(_ollama, _settings);
        _chat.Dock = DockStyle.Fill;
        _chat.OnApplyCodeRequested += (code) => _editor.TextContent = code;
        _splitContainer.Panel2.Controls.Add(_chat);

        // Add Fill control FIRST so docking engine reserves correct space
        Controls.Add(_splitContainer);

        // Sidebar added AFTER — it claims its Left strip from what remains
        _sidebar = new SidebarControl();
        _sidebar.OnViewChanged += OnSidebarViewChanged;
        Controls.Add(_sidebar);
    }

    private void SetSplitterDistance()
    {
        try
        {
            _splitContainer.Panel1MinSize = 300;
            _splitContainer.Panel2MinSize = 250;
            int available = _splitContainer.Width;
            if (available <= 0) return;
            int target = (int)(available * 0.65);
            int clamped = Math.Clamp(target, 300, available - 250);
            _splitContainer.SplitterDistance = clamped;
        }
        catch { /* Ignore if layout not ready */ }
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

            default: // Explorer, Search, AIChat - return to editor
                _settingsControl.Visible = false;
                _editor.Visible = true;
                _editor.BringToFront();
                break;
        }
    }
}
