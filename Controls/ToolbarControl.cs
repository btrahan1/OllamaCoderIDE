using System;
using System.Drawing;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class ToolbarControl : BaseStyledControl
{
    private Label _modelStatusLabel = null!;
    private ComboBox _projectTypeCombo = null!;
    public event Action<string>? OnActionRequested;
    public event Action<string>? OnProjectTypeChanged;


    public void SetModelStatus(string text, Color color)
    {
        if (_modelStatusLabel.InvokeRequired)
        {
            _modelStatusLabel.Invoke((Action)(() => SetModelStatus(text, color)));
            return;
        }
        _modelStatusLabel.Text = text;
        _modelStatusLabel.ForeColor = color;
    }

    public ToolbarControl()
    {
        Dock = DockStyle.Top;
        Height = 44; // Slimmer profile
        BackColor = ThemeManager.Sidebar;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 5, 10, 0)
        };

        var buildBtn = CreateToolbarButton("🔨 Build", Color.FromArgb(40, 160, 80));
        buildBtn.Click += (s, e) => OnActionRequested?.Invoke("build");
        
        var runBtn = CreateToolbarButton("🚀 Run", Color.FromArgb(0, 122, 204));
        runBtn.Click += (s, e) => OnActionRequested?.Invoke("run");

        var testBtn = CreateToolbarButton("🧪 Test", Color.FromArgb(202, 138, 4));
        testBtn.Click += (s, e) => OnActionRequested?.Invoke("test");

        layout.Controls.Add(buildBtn);
        layout.Controls.Add(runBtn);
        layout.Controls.Add(testBtn);

        // Add Spacer
        layout.Controls.Add(new Label { Width = 20, AutoSize = false });

        // Project Type Dropdown
        var label = new Label 
        { 
            Text = "Project:", 
            ForeColor = Color.DarkGray, 
            AutoSize = true, 
            Margin = new Padding(0, 10, 5, 0),
            Font = new Font("Segoe UI", 8.5f)
        };
        layout.Controls.Add(label);

        _projectTypeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Width = 120,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 6, 0, 0)
        };
        _projectTypeCombo.SelectedIndexChanged += (s, e) => {
            if (_projectTypeCombo.SelectedItem is string type)
                OnProjectTypeChanged?.Invoke(type);
        };
        layout.Controls.Add(_projectTypeCombo);

        // Add flexible spacer to push model status to the right
        layout.Controls.Add(new Label { Width = 150, AutoSize = false }); // Simple spacer


        _modelStatusLabel = new Label
        {
            Text = "[OLLAMA] qwen2.5-coder:7b",
            ForeColor = ThemeManager.Primary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(20, 10, 0, 0)
        };
        layout.Controls.Add(_modelStatusLabel);

        Controls.Add(layout);

        // Add a bottom border
        var border = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = ThemeManager.Border
        };
        Controls.Add(border);
    }

    private Button CreateToolbarButton(string text, Color accentColor)
    {
        var btn = new Button
        {
            Text = text,
            Width = 100,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = accentColor;
        
        return btn;
    }

    public void SetProjectTypes(List<string> types, string current)
    {
        _projectTypeCombo.Items.Clear();
        _projectTypeCombo.Items.AddRange(types.ToArray());
        _projectTypeCombo.SelectedItem = current;
    }
}

