using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OllamaCoderIDE.Services;

namespace OllamaCoderIDE.Controls;

public class ChatMessageControl : BaseStyledControl
{
    private readonly string _sender;
    private string _message;
    private RichTextBox _contentBox = null!;
    private TableLayoutPanel _table = null!;
    private List<string> _codes = new();
    private List<ToolCall> _tools = new();
    public event Action<string>? OnApplyCode;
    public event Action<ToolCall>? OnExecuteTool;

    public ChatMessageControl(string sender, string message)
    {
        _sender = sender;
        _message = message;
        // Disable AutoSize here so the parent FlowLayoutPanel can set our Width
        AutoSize = false;
        Padding = new Padding(8);
        Margin = new Padding(0, 0, 0, 8);
        BackColor = sender == "AI" ? ThemeManager.Surface : Color.FromArgb(30, 30, 33);
        InitializeComponents();
    }

    public void Append(string token)
    {
        _message += token;
        _table.SuspendLayout();
        _contentBox.Text = _message;
        _table.ResumeLayout(false);
    }

    public void MarkAsComplete()
    {
        _codes.Clear();
        _tools.Clear();

        // 1. Extract Code Snippets
        var matches = Regex.Matches(_message, @"```(?:csharp|cs|json|)?\n?(.*?)```", RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            var code = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(code)) _codes.Add(code);
        }

        // 2. Extract Tool Calls (Experimental recovery for broken blocks)
        var parseResult = ToolParser.Parse(_message);
        _tools.AddRange(parseResult.Tools);

        // UI: Add buttons for code
        for (int i = 0; i < _codes.Count; i++)
        {
            var idx = i;
            var btn = new ModernButton
            {
                Text = "⬇ Apply Code Snippet",
                Width = 200,
                Height = 34,
                Dock = DockStyle.Left,
                BackColor = ThemeManager.Primary,
                Font = new Font(ThemeManager.TextFont.FontFamily, 8f),
                Margin = new Padding(0, 4, 0, 4)
            };
            btn.Click += (s, e) => OnApplyCode?.Invoke(_codes[idx]);
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            _table.Controls.Add(btn, 0, 2 + _table.Controls.Count);
        }

        // UI: Add buttons for tools
        for (int i = 0; i < _tools.Count; i++)
        {
            var idx = i;
            var btn = new ModernButton
            {
                Text = $"🔧 Execute {_tools[idx].Action}",
                Width = 200,
                Height = 34,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(0, 122, 204), // Blue for tools
                Font = new Font(ThemeManager.TextFont.FontFamily, 8f, FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 4)
            };
            btn.Click += (s, e) => OnExecuteTool?.Invoke(_tools[idx]);
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            _table.Controls.Add(btn, 0, 2 + _table.Controls.Count);
        }
        
        this.Height = _table.PreferredSize.Height + Padding.Vertical + Margin.Vertical;
    }

    private void InitializeComponents()
    {
        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        _table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // content

        // Sender header
        var header = new Label
        {
            Text = _sender,
            Font = new Font(ThemeManager.HeaderFont.FontFamily, 9f, FontStyle.Bold),
            ForeColor = _sender == "AI" ? ThemeManager.Primary : ThemeManager.TextSecondary,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4)
        };
        _table.Controls.Add(header, 0, 0);

        _contentBox = new RichTextBox
        {
            Text = _message,
            Font = ThemeManager.TextFont,
            ForeColor = ThemeManager.TextMain,
            BackColor = _sender == "AI" ? ThemeManager.Surface : Color.FromArgb(30, 30, 33),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.None,
            WordWrap = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4)
        };
        _contentBox.ContentsResized += (s, e) => {
            _contentBox.Height = e.NewRectangle.Height + 4;
            this.Height = _table.PreferredSize.Height + Padding.Vertical + Margin.Vertical;
        };
        _table.Controls.Add(_contentBox, 0, 1);

        _table.Dock = DockStyle.Fill;
        Controls.Add(_table);
    }
}
