using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OllamaCoderIDE.Controls;

public class TerminalControl : BaseStyledControl, IDisposable
{
    private RichTextBox _outputBox = null!;
    private TextBox _inputBox = null!;
    private Process? _process;
    private StreamWriter? _stdin;
    private string? _currentDirectory;

    public void Dispose()
    {
        try {
            if (_process != null && !_process.HasExited) {
                _process.Kill(true); // Kill entire process tree
            }
        } catch { }
        finally {
            _process?.Dispose();
        }
    }

    public TerminalControl()
    {
        Dock = DockStyle.Fill;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = ThemeManager.Sidebar };
        var title = new Label 
        { 
            Text = "TERMINAL", 
            Dock = DockStyle.Left, 
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        
        var stopBtn = new Button
        {
            Text = "⏹ Stop App",
            Dock = DockStyle.Right,
            Width = 90,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(255, 180, 0),
            Font = new Font("Segoe UI", 8f)
        };
        stopBtn.FlatAppearance.BorderSize = 0;
        stopBtn.Click += (s, e) => StopCurrentProcess();

        var killBtn = new Button
        {
            Text = "💀 Reset Terminal",
            Dock = DockStyle.Right,
            Width = 110,
            FlatStyle = FlatStyle.Flat,
            ForeColor = ThemeManager.Error,
            Font = new Font("Segoe UI", 8f)
        };
        killBtn.FlatAppearance.BorderSize = 0;
        killBtn.Click += (s, e) => RestartProcess(_currentDirectory ?? AppContext.BaseDirectory);

        header.Controls.Add(title);
        header.Controls.Add(stopBtn);
        header.Controls.Add(killBtn);

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.LightGray,
            Font = new Font("Consolas", 10f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };

        _inputBox = new TextBox
        {
            Dock = DockStyle.Bottom,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Font = new Font("Consolas", 10f),
            BorderStyle = BorderStyle.FixedSingle
        };
        _inputBox.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Enter) {
                ExecuteCommand(_inputBox.Text);
                _inputBox.Clear();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        Controls.Add(_outputBox);
        Controls.Add(header);
        Controls.Add(_inputBox);
    }

    public void SetWorkingDirectory(string path)
    {
        _currentDirectory = path;
        RestartProcess(path);
    }

    private void RestartProcess(string workingDir)
    {
        try {
            if (_process != null && !_process.HasExited) {
                _process.Kill(true);
            }

            var shell = File.Exists("pwsh.exe") ? "pwsh.exe" : (File.Exists("C:\\Program Files\\PowerShell\\7\\pwsh.exe") ? "C:\\Program Files\\PowerShell\\7\\pwsh.exe" : "powershell.exe");

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = workingDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _process = new Process { StartInfo = psi };
            _process.OutputDataReceived += (s, e) => AppendOutput(e.Data);
            _process.ErrorDataReceived += (s, e) => AppendOutput(e.Data, true);
            
            _process.Start();
            _stdin = _process.StandardInput;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            AppendOutput($"--- Terminal session started in {workingDir} ---");
        } catch (Exception ex) {
            AppendOutput($"Error starting terminal: {ex.Message}", true);
        }
    }

    public void ExecuteCommand(string command)
    {
        if (_stdin != null && !string.IsNullOrEmpty(command)) {
            _stdin.WriteLine(command);
        }
    }

    public void StopCurrentProcess()
    {
        if (_stdin != null)
        {
            // First, try Ctrl+C signal
            _stdin.Write("\u0003");
            _stdin.Flush();

            // Then, aggressively kill any child processes of this shell via PowerShell
            // $PID is the shell's process ID. We kill all its children (-Force).
            ExecuteCommand("Get-CimInstance Win32_Process -Filter \"ParentProcessId = $PID\" | Stop-Process -Force -ErrorAction SilentlyContinue");

            AppendOutput("\n^C (Aggressive Stop signal sent)");
        }
    }

    private void AppendOutput(string? text, bool isError = false)
    {
        if (text == null) return;
        if (InvokeRequired) {
            Invoke(new Action(() => AppendOutput(text, isError)));
            return;
        }

        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.SelectionLength = 0;
        _outputBox.SelectionColor = isError ? ThemeManager.Error : Color.LightGray;
        _outputBox.AppendText(text + Environment.NewLine);
        _outputBox.ScrollToCaret();
    }

    public async Task<string> RunCommandAndCapture(string command, int timeoutMs = 5000, CancellationToken ct = default)
    {
        var outputBuilder = new StringBuilder();
        DataReceivedEventHandler handler = (s, e) => {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        _process!.OutputDataReceived += handler;
        _process!.ErrorDataReceived += handler;

        ExecuteCommand(command);
        
        try
        {
            await Task.Delay(timeoutMs, ct); // Respect cancellation
        }
        catch (TaskCanceledException)
        {
            StopCurrentProcess(); // Aggressively stop if task cancelled
        }
        finally
        {
            _process!.OutputDataReceived -= handler;
            _process!.ErrorDataReceived -= handler;
        }

        return outputBuilder.ToString();
    }
}
