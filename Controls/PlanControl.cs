using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Controls;

public class PlanControl : BaseStyledControl
{
    private FlowLayoutPanel _taskListPanel = null!;
    private List<PlanItem> _items = new();
    private Dictionary<PlanItem, ModernButton> _taskButtons = new();
    
    public event Action<PlanItem>? OnExecuteTask;

    public PlanControl()
    {
        Dock = DockStyle.Fill;
        InitializePlanUI();
    }

    private void InitializePlanUI()
    {
        var header = new Label
        {
            Text = "📋 Implementation Plan",
            Dock = DockStyle.Top,
            Height = 45,
            Font = ThemeManager.HeaderFont,
            ForeColor = ThemeManager.TextMain,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0)
        };

        _taskListPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(10)
        };

        _taskListPanel.Resize += (s, e) => {
            foreach (Control c in _taskListPanel.Controls) c.Width = _taskListPanel.Width - 30;
        };

        Controls.Add(_taskListPanel);
        Controls.Add(header);
    }

    public void LoadPlan(string planText)
    {
        _items.Clear();
        _taskButtons.Clear();
        _taskListPanel.Controls.Clear();

        // Simple parsing: Look for lines starting with "- [ ]" or digits like "1."
        var lines = planText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            string clean = line.Trim();
            if (clean.StartsWith("- [ ]") || clean.StartsWith("- ") || char.IsDigit(clean.FirstOrDefault()))
            {
                var item = new PlanItem { Title = clean.TrimStart('-', '[', ']', ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.') };
                _items.Add(item);
                AddTaskControl(item);
            }
        }
    }

    private void AddTaskControl(PlanItem item)
    {
        var container = new Panel
        {
            Height = 80,
            Width = _taskListPanel.Width - 30,
            BackColor = ThemeManager.Sidebar,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(10)
        };

        var titleLabel = new Label
        {
            Text = item.Title,
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = ThemeManager.TextMain,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var executeBtn = new ModernButton
        {
            Text = "🚀 Execute Step",
            Dock = DockStyle.Bottom,
            Height = 35,
            Width = 120,
            BackColor = ThemeManager.Primary
        };

        executeBtn.Click += (s, e) => {
            item.Status = PlanItemStatus.InProgress;
            executeBtn.Enabled = false;
            executeBtn.Text = "⏳ Running...";
            OnExecuteTask?.Invoke(item);
        };

        _taskButtons[item] = executeBtn;

        container.Controls.Add(executeBtn);
        container.Controls.Add(titleLabel);
        
        _taskListPanel.Controls.Add(container);
    }

    public void MarkItemCompleted(PlanItem item)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(() => MarkItemCompleted(item)));
            return;
        }

        if (_taskButtons.TryGetValue(item, out var btn))
        {
            item.Status = PlanItemStatus.Completed;
            btn.Text = "✅ Complete";
            btn.BackColor = ThemeManager.Success;
            btn.Enabled = false;
        }
    }
}
