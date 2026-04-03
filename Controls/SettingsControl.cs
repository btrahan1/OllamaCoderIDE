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
    private readonly OllamaService _ollamaService;
    private ComboBox _modelCombo = null!;
    private TextBox _systemPromptBox = null!;
    private NumericUpDown _tempNum = null!;
    private NumericUpDown _topKNum = null!;
    private NumericUpDown _topPNum = null!;
    private NumericUpDown _ctxNum = null!;
    private NumericUpDown _historyNum = null!;
    private CheckBox _autoExecCheck = null!;

    public SettingsControl(SettingsService settingsService, OllamaService ollamaService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;
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

        // Model Selection
        layout.Controls.Add(CreateLabel("Selected Model:"));
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
            Text = "Refresh Models",
            Width = 150,
            Height = 30,
            Margin = new Padding(0, 5, 0, 15)
        };
        refreshBtn.Click += async (s, e) => await RefreshModels();
        layout.Controls.Add(refreshBtn);

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
        
        if (_modelCombo.Items.Contains(s.SelectedModel))
            _modelCombo.SelectedItem = s.SelectedModel;
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
        var models = await _ollamaService.GetModelsAsync();
        _modelCombo.Invoke((Action)(() => {
            _modelCombo.Items.Clear();
            foreach (var m in models) _modelCombo.Items.Add(m);
            if (_modelCombo.Items.Contains(_settingsService.Current.SelectedModel))
                _modelCombo.SelectedItem = _settingsService.Current.SelectedModel;
            else if (_modelCombo.Items.Count > 0)
                _modelCombo.SelectedIndex = 0;
        }));
    }
}
