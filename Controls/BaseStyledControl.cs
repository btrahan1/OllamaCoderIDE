using System.Windows.Forms;
using System.Drawing;

namespace OllamaCoderIDE.Controls;

public class BaseStyledControl : UserControl
{
    public BaseStyledControl()
    {
        BackColor = ThemeManager.Background;
        ForeColor = ThemeManager.TextMain;
        Font = ThemeManager.TextFont;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Custom painting can be added here for borders etc.
        using var pen = new Pen(ThemeManager.Border, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
