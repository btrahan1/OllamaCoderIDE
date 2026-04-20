using System;
using System.Drawing;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public enum SidebarView
{
    Explorer,
    Search,
    AIChat,
    Plan,
    Settings
}

public class SidebarControl : BaseStyledControl
{
    private readonly Panel _buttonContainer;
    private readonly Dictionary<SidebarView, Button> _buttons = new();
    private SidebarView _activeView = SidebarView.Explorer;
    public event Action<SidebarView>? OnViewChanged;

    public SidebarControl()
    {
        Dock = DockStyle.Left;
        Width = 60; 
        BackColor = ThemeManager.Sidebar;
        Padding = new Padding(0, 20, 0, 0);

        _buttonContainer = new Panel { Dock = DockStyle.Fill };
        Controls.Add(_buttonContainer);

        // Segoe MDL2 Assets: Settings: \uE713, Plan: \uE8FD, Chat: \uE8BD, Search: \uE721, Explorer: \uE8B7
        AddSidebarButton("\uE713", "Settings", SidebarView.Settings);
        AddSidebarButton("\uE8FD", "Plan", SidebarView.Plan);
        AddSidebarButton("\uE8BD", "AI Chat", SidebarView.AIChat);
        AddSidebarButton("\uE721", "Search", SidebarView.Search);
        AddSidebarButton("\uE8B7", "Explorer", SidebarView.Explorer);

        SetActive(SidebarView.Explorer);
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
            Font = new Font("Segoe MDL2 Assets", 14f),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 45, 50);
        btn.Click += (s, e) => {
            SetActive(view);
            OnViewChanged?.Invoke(view);
        };

        var tt = new ToolTip();
        tt.SetToolTip(btn, tooltip);

        _buttons[view] = btn;
        _buttonContainer.Controls.Add(btn);
    }

    private void SetActive(SidebarView view)
    {
        _activeView = view;
        foreach (var kv in _buttons)
        {
            bool isActive = kv.Key == view;
            kv.Value.BackColor = isActive ? ThemeManager.ActiveAccent : Color.Transparent;
            kv.Value.ForeColor = isActive ? Color.White : ThemeManager.TextSecondary;
        }
    }
}
