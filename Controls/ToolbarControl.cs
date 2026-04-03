using System;
using System.Drawing;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class ToolbarControl : BaseStyledControl
{
    public event Action<string>? OnActionRequested;

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
}
