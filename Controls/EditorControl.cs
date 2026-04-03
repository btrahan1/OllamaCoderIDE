using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class EditorControl : BaseStyledControl
{
    public RichTextBox TextBox { get; private set; } = null!;

    public EditorControl()
    {
        Dock = DockStyle.Fill;
        InitializeEditor();
    }

    private void InitializeEditor()
    {
        TextBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Background,
            ForeColor = ThemeManager.TextMain,
            Font = ThemeManager.CodeFont,
            BorderStyle = BorderStyle.None,
            AcceptsTab = true,
            Multiline = true,
            Padding = new Padding(10)
        };

        // Add a simple margin/border padding
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        panel.Controls.Add(TextBox);
        Controls.Add(panel);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string TextContent
    {
        get => TextBox.Text;
        set => TextBox.Text = value;
    }
}
