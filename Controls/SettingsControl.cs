using System;
using System.Drawing;
using System.Windows.Forms;
using OllamaCoderIDE.Services;
using OllamaCoderIDE.Models;
using System.Threading.Tasks;

namespace OllamaCoderIDE.Controls;

public class SettingsControl : BaseStyledControl
{
    private readonly SettingsService _settingsService;
    private readonly ILLMService _ollamaService;
    private readonly ILLMService _geminiService;
    private ComboBox _providerCombo = null!;
    private ComboBox _modelCombo = null!;
    private TextBox _geminiKeyBox = null!;
    private TextBox _geminiModelBox = null!;
    private TextBox _systemPromptBox = null!;
    private Panel _geminiSettingsPanel = null!;
    private NumericUpDown _tempNum = null!;
    private NumericUpDown _topKNum = null!;
    private NumericUpDown _topPNum = null!;
    private NumericUpDown _ctxNum = null!;
    private NumericUpDown _historyNum = null!;
    private CheckBox _autoExecCheck = null!;
    private CheckBox _fullFileModeCheck = null!;

    public SettingsControl(SettingsService settingsService, ILLMService ollamaService, ILLMService geminiService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;
        _geminiService = geminiService;
        Dock = DockStyle.Fill;
        Padding = new Padding(40);
        InitializeSettings();
        LoadCurrentSettings();
    }

    private void InitializeSettings()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        layout.Controls.Add(CreateHeader("Application Settings"));
        layout.Controls.Add(CreateSpacer(20));

        // Provider Selection
        layout.Controls.Add(CreateLabel("Model Provider:"));
        _providerCombo = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain,
            FlatStyle = FlatStyle.Flat
        };
        _providerCombo.Items.AddRange(Enum.GetNames(typeof(LlmProvider)));
        _providerCombo.SelectedIndexChanged += (s, e) => {
            if (Enum.TryParse<LlmProvider>(_providerCombo.SelectedItem.ToString(), out var p))
            {
                _settingsService.Current.Provider = p;
                ToggleProviderUI(p);
                _ = RefreshModels();
            }
        };
        layout.Controls.Add(_providerCombo);

        // Ollama specific
        layout.Controls.Add(CreateLabel("Selected Model (Ollama):"));
        _modelCombo = new ComboBox
        {
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain,
            FlatStyle = FlatStyle.Flat
        };
        _modelCombo.SelectedIndexChanged += (s, e) => {
            if (_modelCombo.SelectedItem is string model)
                _settingsService.Current.SelectedModel = model;
        };
        layout.Controls.Add(_modelCombo);

        var refreshBtn = new ModernButton
        {
            Text = "Refresh Ollama Models",
            Width = 180,
            Height = 30,
            Margin = new Padding(0, 5, 0, 15)
        };
        refreshBtn.Click += async (s, e) => await RefreshModels();
        layout.Controls.Add(refreshBtn);

        // Gemini specific Panel
        _geminiSettingsPanel = new FlowLayoutPanel { Width = 450, Height = 140, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        
        _geminiSettingsPanel.Controls.Add(CreateLabel("Gemini API Key:"));
        _geminiKeyBox = new TextBox { Width = 400, BackColor = ThemeManager.Surface, ForeColor = ThemeManager.TextMain, PasswordChar = '*' };
        _geminiKeyBox.TextChanged += (s, e) => _settingsService.Current.GeminiApiKey = _geminiKeyBox.Text;
        _geminiSettingsPanel.Controls.Add(_geminiKeyBox);

        _geminiSettingsPanel.Controls.Add(CreateLabel("Gemini Model Name:"));
        _geminiModelBox = new TextBox { Width = 400, BackColor = ThemeManager.Surface, ForeColor = ThemeManager.TextMain };
        _geminiModelBox.TextChanged += (s, e) => _settingsService.Current.GeminiModel = _geminiModelBox.Text;
        _geminiSettingsPanel.Controls.Add(_geminiModelBox);
        
        layout.Controls.Add(_geminiSettingsPanel);

        // Numeric Settings
        _tempNum = CreateNumericControl("Temperature:", 0, 2, 0.1m, layout);
        _topKNum = CreateNumericControl("Top K:", 0, 100, 1, layout);
        _topPNum = CreateNumericControl("Top P:", 0, 1, 0.05m, layout);
        _ctxNum = CreateNumericControl("Context Size:", 1024, 128000, 1024, layout);

        // Agentic Settings
        layout.Controls.Add(CreateLabel("Agent System Prompt:"));
        _systemPromptBox = new TextBox
        {
            Width = 450,
            Height = 150,
            Multiline = true,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = ScrollBars.Vertical
        };
        _systemPromptBox.TextChanged += (s, e) => _settingsService.Current.AgentSystemPrompt = _systemPromptBox.Text;
        layout.Controls.Add(_systemPromptBox);

        _historyNum = CreateNumericControl("Max History Messages:", 1, 100, 1, layout);

        _autoExecCheck = new CheckBox
        {
            Text = "Auto-Execute Tools (Advanced)",
            ForeColor = ThemeManager.TextMain,
            AutoSize = true,
            Margin = new Padding(0, 15, 0, 0)
        };
        _autoExecCheck.CheckedChanged += (s, e) => _settingsService.Current.AutoExecuteTools = _autoExecCheck.Checked;
        layout.Controls.Add(_autoExecCheck);
        
        _fullFileModeCheck = new CheckBox
        {
            Text = "Full File Replacement only",
            ForeColor = ThemeManager.TextMain,
            AutoSize = true,
            Margin = new Padding(0, 15, 0, 0)
        };
        _fullFileModeCheck.CheckedChanged += (s, e) => _settingsService.Current.FullFileReplacementOnly = _fullFileModeCheck.Checked;
        layout.Controls.Add(_fullFileModeCheck);

        var footerPanel = new FlowLayoutPanel { Width = 450, Height = 60, Margin = new Padding(0, 30, 0, 0) };
        
        var saveBtn = new ModernButton
        {
            Text = "Save Settings",
            Width = 180,
            Height = 45
        };
        saveBtn.Click += (s, e) => {
            _settingsService.Save();
            MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var resetBtn = new ModernButton
        {
            Text = "Reset to Defaults",
            Width = 180,
            Height = 45,
            BackColor = Color.FromArgb(60, 60, 65),
            Margin = new Padding(20, 0, 0, 0)
        };
        resetBtn.Click += (s, e) => {
            if (MessageBox.Show("Are you sure you want to reset all settings to their defaults?", "Reset Settings", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                _settingsService.ResetToDefaults();
                LoadCurrentSettings();
            }
        };

        footerPanel.Controls.Add(saveBtn);
        footerPanel.Controls.Add(resetBtn);
        layout.Controls.Add(footerPanel);

        Controls.Add(layout);
        _ = RefreshModels();
    }

    private void LoadCurrentSettings()
    {
        var s = _settingsService.Current;
        _tempNum.Value = (decimal)s.Temperature;
        _topKNum.Value = s.TopK;
        _topPNum.Value = (decimal)s.TopP;
        _ctxNum.Value = s.NumCtx;
        _systemPromptBox.Text = s.AgentSystemPrompt;
        _historyNum.Value = s.MaxHistoryMessages;
        _autoExecCheck.Checked = s.AutoExecuteTools;
        _fullFileModeCheck.Checked = s.FullFileReplacementOnly;
        
        _providerCombo.SelectedItem = s.Provider.ToString();
        _geminiKeyBox.Text = s.GeminiApiKey;
        _geminiModelBox.Text = s.GeminiModel;

        if (_modelCombo.Items.Contains(s.SelectedModel))
            _modelCombo.SelectedItem = s.SelectedModel;

        ToggleProviderUI(s.Provider);
    }

    private void ToggleProviderUI(LlmProvider provider)
    {
        _geminiSettingsPanel.Visible = (provider == LlmProvider.Gemini);
        _modelCombo.Enabled = (provider == LlmProvider.Ollama);
    }

    private NumericUpDown CreateNumericControl(string labelText, decimal min, decimal max, decimal inc, FlowLayoutPanel layout)
    {
        layout.Controls.Add(CreateLabel(labelText));
        var num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = inc,
            DecimalPlaces = inc % 1 == 0 ? 0 : 2,
            Width = 120,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain
        };
        num.ValueChanged += (s, e) => {
            var curr = _settingsService.Current;
            if (labelText.Contains("Temperature")) curr.Temperature = (double)num.Value;
            else if (labelText.Contains("Top K")) curr.TopK = (int)num.Value;
            else if (labelText.Contains("Top P")) curr.TopP = (double)num.Value;
            else if (labelText.Contains("Context")) curr.NumCtx = (int)num.Value;
            else if (labelText.Contains("Max History")) curr.MaxHistoryMessages = (int)num.Value;
        };
        layout.Controls.Add(num);
        return num;
    }

    private Control CreateHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = ThemeManager.Primary,
            AutoSize = true
        };
    }

    private Control CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = ThemeManager.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0, 15, 0, 5)
        };
    }

    private Control CreateSpacer(int height) => new Control { Height = height };

    private async Task RefreshModels()
    {
        var provider = _settingsService.Current.Provider;
        var service = (provider == LlmProvider.Ollama) ? _ollamaService : _geminiService;
        
        var models = await service.GetModelsAsync();
        _modelCombo.Invoke((Action)(() => {
            _modelCombo.Items.Clear();
            foreach (var m in models) _modelCombo.Items.Add(m);
            
            if (provider == LlmProvider.Ollama)
            {
                if (_modelCombo.Items.Contains(_settingsService.Current.SelectedModel))
                    _modelCombo.SelectedItem = _settingsService.Current.SelectedModel;
                else if (_modelCombo.Items.Count > 0)
                    _modelCombo.SelectedIndex = 0;
            }
            else
            {
                _modelCombo.SelectedItem = _settingsService.Current.GeminiModel;
            }
        }));
    }
}
