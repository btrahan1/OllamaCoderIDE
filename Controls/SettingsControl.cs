using System;
using System.Drawing;
using System.Windows.Forms;
using OllamaCoderIDE.Services;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Controls;

public class SettingsControl : BaseStyledControl
{
    private readonly SettingsService _settingsService;
    private readonly OllamaService _ollamaService;
    private ComboBox _modelCombo = null!;

    public SettingsControl(SettingsService settingsService, OllamaService ollamaService)
    {
        _settingsService = settingsService;
        _ollamaService = ollamaService;
        Dock = DockStyle.Fill;
        Padding = new Padding(40);
        InitializeSettings();
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
        layout.Controls.Add(CreateNumericSetting("Temperature:", 0, 2, 0.1m, _settingsService.Current.Temperature, (val) => _settingsService.Current.Temperature = (double)val));
        layout.Controls.Add(CreateNumericSetting("Top K:", 0, 100, 1, _settingsService.Current.TopK, (val) => _settingsService.Current.TopK = (int)val));
        layout.Controls.Add(CreateNumericSetting("Top P:", 0, 1, 0.05m, _settingsService.Current.TopP, (val) => _settingsService.Current.TopP = (double)val));
        layout.Controls.Add(CreateNumericSetting("Context Size:", 1024, 32768, 1024, _settingsService.Current.NumCtx, (val) => _settingsService.Current.NumCtx = (int)val));

        var saveBtn = new ModernButton
        {
            Text = "Save Settings",
            Width = 200,
            Height = 45,
            Margin = new Padding(0, 30, 0, 0)
        };
        saveBtn.Click += (s, e) => {
            _settingsService.Save();
            MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        layout.Controls.Add(saveBtn);

        Controls.Add(layout);
        _ = RefreshModels();
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

    private Control CreateNumericSetting(string labelText, decimal min, decimal max, decimal inc, double currentVal, Action<decimal> onValChanged)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(CreateLabel(labelText));
        var num = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = inc,
            Value = (decimal)currentVal,
            DecimalPlaces = inc % 1 == 0 ? 0 : 2,
            Width = 100,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain
        };
        num.ValueChanged += (s, e) => onValChanged(num.Value);
        panel.Controls.Add(num);
        return panel;
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
