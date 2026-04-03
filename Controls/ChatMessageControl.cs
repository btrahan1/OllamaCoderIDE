using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class ChatMessageControl : BaseStyledControl
{
    private readonly string _sender;
    private string _message;
    private RichTextBox _contentBox = null!;
    private TableLayoutPanel _table = null!;
    private List<string> _codes = new();
    public event Action<string>? OnApplyCode;

    public ChatMessageControl(string sender, string message)
    {
        _sender = sender;
        _message = message;
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
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
        // Re-extract code blocks and add buttons
        _codes.Clear();
        var matches = Regex.Matches(_message, @"```(?:csharp|cs|)?\n?(.*?)```", RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            var code = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(code)) _codes.Add(code);
        }

        // Add buttons if codes found
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
            _table.Controls.Add(btn, 0, 2 + i);
        }
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
        _contentBox.ContentsResized += (s, e) => _contentBox.Height = e.NewRectangle.Height + 4;
        _table.Controls.Add(_contentBox, 0, 1);

        Controls.Add(_table);
    }
}
