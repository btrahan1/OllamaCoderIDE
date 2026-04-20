using System;
using System.Drawing;
using System.Windows.Forms;
using ScintillaNET;
using OllamaCoderIDE.Services;
using System.Collections.Generic;

namespace OllamaCoderIDE.Controls;

public class DiffDialog : Form
{
    private Scintilla _leftScintilla = null!;
    private Scintilla _rightScintilla = null!;
    private ModernButton _applyButton = null!;
    private ModernButton _rejectButton = null!;
    private readonly string _filePath;

    public DiffDialog(string filePath, string oldContent, string newContent)
    {
        _filePath = filePath;
        InitializeComponent();
        LoadDiff(oldContent, newContent);
    }

    private void InitializeComponent()
    {
        this.Text = $"Review Changes: {System.IO.Path.GetFileName(_filePath)}";
        this.Size = new Size(1100, 750);
        this.BackColor = ThemeManager.Background;
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowInTaskbar = false;
        this.FormBorderStyle = FormBorderStyle.SizableToolWindow;

        var header = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(15, 0, 15, 0) };
        var title = new Label
        {
            Text = $"Proposed Changes for {System.IO.Path.GetFileName(_filePath)}",
            Dock = DockStyle.Fill,
            ForeColor = ThemeManager.TextMain,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(title);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _leftScintilla = CreateDiffEditor("Original");
        _rightScintilla = CreateDiffEditor("Proposed");

        table.Controls.Add(_leftScintilla, 0, 0);
        table.Controls.Add(_rightScintilla, 1, 0);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(0, 5, 20, 5) };
        _applyButton = new ModernButton
        {
            Text = "Apply Changes",
            Dock = DockStyle.Right,
            Width = 150,
            BackColor = ThemeManager.Success
        };
        _applyButton.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

        _rejectButton = new ModernButton
        {
            Text = "Reject",
            Dock = DockStyle.Right,
            Width = 100,
            BackColor = Color.FromArgb(70, 40, 40),
            Margin = new Padding(0, 0, 10, 0)
        };
        _rejectButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        
        var spacer = new Panel { Dock = DockStyle.Right, Width = 15 };

        footer.Controls.Add(_applyButton);
        footer.Controls.Add(spacer);
        footer.Controls.Add(_rejectButton);

        this.Controls.Add(table);
        this.Controls.Add(header);
        this.Controls.Add(footer);

        // Sync scrolling
        _leftScintilla.UpdateUI += (s, e) => {
            const int SC_UPDATE_VSCROLL = 8;
            const int SC_UPDATE_HSCROLL = 4;
            if (((int)e.Change & SC_UPDATE_HSCROLL) != 0 || ((int)e.Change & SC_UPDATE_VSCROLL) != 0)
                SyncScroll(_leftScintilla, _rightScintilla);
        };
    }

    private Scintilla CreateDiffEditor(string title)
    {
        var sc = new Scintilla
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            LexerName = "cpp"
        };
        
        // Setup styling (simplified version of EditorControl's setup)
        sc.StyleResetDefault();
        sc.Styles[Style.Default].Font = ThemeManager.CodeFont.Name;
        sc.Styles[Style.Default].Size = (int)ThemeManager.CodeFont.Size;
        sc.Styles[Style.Default].BackColor = ThemeManager.Background;
        sc.Styles[Style.Default].ForeColor = ThemeManager.TextMain;
        sc.StyleClearAll();

        sc.Margins[0].Width = 40;
        sc.Styles[Style.LineNumber].BackColor = ThemeManager.Surface;
        sc.Styles[Style.LineNumber].ForeColor = ThemeManager.TextSecondary;

        // Custom markers for diff
        sc.Markers[1].Symbol = MarkerSymbol.Background;
        sc.Markers[1].SetBackColor(Color.FromArgb(60, 40, 40)); // Deletion Red
        
        sc.Markers[2].Symbol = MarkerSymbol.Background;
        sc.Markers[2].SetBackColor(Color.FromArgb(40, 60, 40)); // Addition Green

        return sc;
    }

    private void LoadDiff(string oldContent, string newContent)
    {
        var service = new DiffService();
        var diff = service.ComputeDiff(oldContent, newContent);

        var leftLines = new List<string>();
        var rightLines = new List<string>();

        foreach (var line in diff)
        {
            switch (line.Type)
            {
                case DiffType.Equal:
                    leftLines.Add(line.Text);
                    rightLines.Add(line.Text);
                    break;
                case DiffType.Delete:
                    leftLines.Add(line.Text);
                    rightLines.Add(""); // Gap on the right
                    break;
                case DiffType.Insert:
                    leftLines.Add(""); // Gap on the left
                    rightLines.Add(line.Text);
                    break;
            }
        }

        _leftScintilla.ReadOnly = false;
        _leftScintilla.Text = string.Join("\n", leftLines);
        _leftScintilla.ReadOnly = true;

        _rightScintilla.ReadOnly = false;
        _rightScintilla.Text = string.Join("\n", rightLines);
        _rightScintilla.ReadOnly = true;

        // Highlight
        for (int i = 0; i < diff.Count; i++)
        {
            if (diff[i].Type == DiffType.Delete)
                _leftScintilla.Lines[i].MarkerAdd(1);
            if (diff[i].Type == DiffType.Insert)
                _rightScintilla.Lines[i].MarkerAdd(2);
        }
    }

    private void SyncScroll(Scintilla source, Scintilla target)
    {
        int firstLine = source.FirstVisibleLine;
        if (target.FirstVisibleLine != firstLine)
        {
            target.LineScroll(firstLine - target.FirstVisibleLine, 0);
        }
    }
}
