using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class ModernButton : Button
{
    private bool _isHovered = false;
    private bool _isPressed = false;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = ThemeManager.Primary;
        ForeColor = Color.White;
        Font = ThemeManager.TextFont;
        Cursor = Cursors.Hand;
        Size = new Size(120, 40);
        
        MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
        MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
        MouseDown += (s, e) => { _isPressed = true; Invalidate(); };
        MouseUp += (s, e) => { _isPressed = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color currentBg = _isPressed ? ThemeManager.PrimaryHover : 
                         (_isHovered ? ThemeManager.PrimaryHover : BackColor);
        
        using (var brush = new SolidBrush(currentBg))
        using (var path = GetRoundedRectangle(ClientRectangle, 8))
        {
            g.FillPath(brush, path);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, 
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
