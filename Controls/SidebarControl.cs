using System;
using System.Drawing;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public enum SidebarView
{
    Explorer,
    Search,
    AIChat,
    Settings
}

public class SidebarControl : BaseStyledControl
{
    private readonly Panel _buttonContainer;
    public event Action<SidebarView>? OnViewChanged;

    public SidebarControl()
    {
        Dock = DockStyle.Left;
        Width = 60; // Narrow iconic sidebar
        BackColor = ThemeManager.Sidebar;
        Padding = new Padding(0, 20, 0, 0);

        _buttonContainer = new Panel { Dock = DockStyle.Fill };
        Controls.Add(_buttonContainer);

        AddSidebarButton("⚙️", "Settings", SidebarView.Settings);
        AddSidebarButton("🤖", "AI Chat", SidebarView.AIChat);
        AddSidebarButton("🔍", "Search", SidebarView.Search);
        AddSidebarButton("📄", "Explorer", SidebarView.Explorer);
    }

    private void AddSidebarButton(string icon, string tooltip, SidebarView view)
    {
        var btn = new Button
        {
            Text = icon,
            Dock = DockStyle.Top,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI Emoji", 14f),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 50);
        btn.Click += (s, e) => OnViewChanged?.Invoke(view);

        _buttonContainer.Controls.Add(btn);
    }
}
