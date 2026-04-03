using System.Drawing;

namespace OllamaCoderIDE;

public static class ThemeManager
{
    // Modern Dark Theme Colors
    public static readonly Color Background = Color.FromArgb(18, 18, 18);
    public static readonly Color Surface = Color.FromArgb(24, 24, 27);
    public static readonly Color Sidebar = Color.FromArgb(31, 31, 35);
    public static readonly Color Border = Color.FromArgb(39, 39, 42);
    
    public static readonly Color Primary = Color.FromArgb(59, 130, 246); // Blue-500
    public static readonly Color PrimaryHover = Color.FromArgb(37, 99, 235);
    
    public static readonly Color TextMain = Color.FromArgb(228, 228, 231);
    public static readonly Color TextSecondary = Color.FromArgb(161, 161, 170);
    
    public static readonly Color Success = Color.FromArgb(34, 197, 94);
    public static readonly Color Warning = Color.FromArgb(234, 179, 8);
    public static readonly Color Error = Color.FromArgb(239, 68, 68);

    public static readonly Font HeaderFont = new Font("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font TextFont = new Font("Segoe UI", 10f, FontStyle.Regular);
    public static readonly Font CodeFont = new Font("Consolas", 11f, FontStyle.Regular);
}
