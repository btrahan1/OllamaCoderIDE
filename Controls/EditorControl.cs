using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using ScintillaNET;

namespace OllamaCoderIDE.Controls;

public class EditorControl : BaseStyledControl
{
    private Scintilla _scintilla = null!;
    private Label _titleLabel = null!;
    private string? _currentFilePath;

    public EditorControl()
    {
        Dock = DockStyle.Fill;
        InitializeEditor();
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            _currentFilePath = value;
            _titleLabel.Text = string.IsNullOrEmpty(value) ? "No file open" : $"📄 {Path.GetFileName(value)}";
        }
    }

    private void InitializeEditor()
    {
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = ThemeManager.Sidebar,
            Padding = new Padding(10, 0, 0, 0)
        };

        _titleLabel = new Label
        {
            Text = "No file open",
            Dock = DockStyle.Fill,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        headerPanel.Controls.Add(_titleLabel);

        _scintilla = new Scintilla
        {
            Dock = DockStyle.Fill,
            LexerName = "cpp"
        };

        SetupScintilla();

        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1)
        };
        container.Controls.Add(_scintilla);
        
        Controls.Add(container);
        Controls.Add(headerPanel);
    }

    private void SetupScintilla()
    {
        // Default style
        _scintilla.StyleResetDefault();
        _scintilla.Styles[Style.Default].Font = ThemeManager.CodeFont.Name;
        _scintilla.Styles[Style.Default].Size = (int)ThemeManager.CodeFont.Size;
        _scintilla.Styles[Style.Default].BackColor = ThemeManager.Background;
        _scintilla.Styles[Style.Default].ForeColor = ThemeManager.TextMain;
        _scintilla.StyleClearAll();

        // Line numbers
        _scintilla.Margins[0].Width = 40;
        _scintilla.Styles[Style.LineNumber].BackColor = ThemeManager.Surface;
        _scintilla.Styles[Style.LineNumber].ForeColor = ThemeManager.TextSecondary;

        // Styling based on indices if named constants fail
        // CPP/C# Lexer Style Indices:
        // 1 = Comment, 2 = Line Comment, 4 = Number, 5 = Keyword, 6 = String, 10 = Operator, 16 = Keyword2 (Types)
        
        _scintilla.Styles[1].ForeColor = ThemeManager.SyntaxComment; // Comment
        _scintilla.Styles[2].ForeColor = ThemeManager.SyntaxComment; // Line Comment
        _scintilla.Styles[4].ForeColor = ThemeManager.SyntaxNumber;  // Number
        _scintilla.Styles[5].ForeColor = ThemeManager.SyntaxKeyword; // Keyword
        _scintilla.Styles[6].ForeColor = ThemeManager.SyntaxString;  // String
        _scintilla.Styles[10].ForeColor = ThemeManager.TextMain;     // Operator
        _scintilla.Styles[16].ForeColor = ThemeManager.SyntaxType;    // KeywordSet 2 (Types)

        _scintilla.SetKeywords(0, "abstract as base break case catch checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while");
        _scintilla.SetKeywords(1, "bool byte char DateTime decimal double float int long sbyte short string uint ulong ushort void");

        // Caret and Selection
        _scintilla.CaretForeColor = Color.White;
        _scintilla.SelectionBackColor = Color.FromArgb(60, 60, 70);

        // Basic Tab behavior
        _scintilla.TabWidth = 4;
        _scintilla.UseTabs = false;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string TextContent
    {
        get => _scintilla.Text;
        set => _scintilla.Text = value;
    }

    public void SetLanguage(string extension)
    {
        switch (extension.ToLower())
        {
            case ".py":
                _scintilla.LexerName = "python";
                break;
            default:
                _scintilla.LexerName = "cpp";
                break;
        }
    }
}
