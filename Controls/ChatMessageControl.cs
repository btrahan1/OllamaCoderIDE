using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class ChatMessageControl : BaseStyledControl
{
    private readonly string _sender;
    private readonly string _message;
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

    private void InitializeComponents()
    {
        // Extract code blocks
        var codes = new List<string>();
        var matches = Regex.Matches(_message, @"```(?:csharp|cs|)?\n?(.*?)```", RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            var code = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(code)) codes.Add(code);
        }

        int rows = 2 + codes.Count; // header + content + N buttons
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // content
        for (int i = 0; i < codes.Count; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));

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
        table.Controls.Add(header, 0, 0);

        // Selectable content box — full width via TableLayoutPanel column
        var contentBox = new RichTextBox
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
        // Auto-size the RichTextBox height to its content
        contentBox.ContentsResized += (s, e) => contentBox.Height = e.NewRectangle.Height + 4;
        table.Controls.Add(contentBox, 0, 1);

        // Apply button for each code block found
        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i]; // capture for closure
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
            btn.Click += (s, e) => OnApplyCode?.Invoke(code);
            table.Controls.Add(btn, 0, 2 + i);
        }

        Controls.Add(table);
    }
}
