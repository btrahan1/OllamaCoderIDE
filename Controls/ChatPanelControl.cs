using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using OllamaCoderIDE.Services;
using OllamaCoderIDE.Models;

namespace OllamaCoderIDE.Controls;

public class ChatPanelControl : BaseStyledControl
{
    private ILLMService _currentService;
    private readonly SettingsService _settingsService;
    private readonly TerminalControl _terminal;
    private TabControl _contentTabs = null!;
    private FlowLayoutPanel _chatHistoryContainer = null!;
    private RichTextBox _reasoningBox = null!;
    private TextBox _promptInput = null!;
    private ModernButton _sendButton = null!;
    private Label _timerLabel = null!;
    private ModernButton _resetButton = null!;
    private RichTextBox _promptLogBox = null!;
    private System.Threading.CancellationTokenSource? _cts;
    public event Action<string>? OnApplyCodeRequested;
    public event Action<string>? OnFileModified;

    public ChatPanelControl(ILLMService service, SettingsService settingsService, TerminalControl terminal)
    {
        _currentService = service;
        _settingsService = settingsService;
        _terminal = terminal;
        Dock = DockStyle.Fill;
        Padding = new Padding(15);
        InitializeChat();
        SubscribeToPrompts();
    }

    private void SubscribeToPrompts()
    {
        _currentService.OnPromptSent += (log) => {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AppendPromptLog(log)));
            }
            else
            {
                AppendPromptLog(log);
            }
        };
    }

    private void AppendPromptLog(string log)
    {
        _promptLogBox.AppendText($"--- PROMPT SENT AT {DateTime.Now:HH:mm:ss} ---\n");
        _promptLogBox.AppendText(log + "\n\n");
        _promptLogBox.ScrollToCaret();
    }

    public void UpdateService(ILLMService service)
    {
        _currentService = service;
        SubscribeToPrompts(); // Re-hook for the new provider
    }

    private void InitializeChat()
    {
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 45 };
        var header = new Label
        {
            Text = "Ollama Agent",
            Dock = DockStyle.Left,
            Font = ThemeManager.HeaderFont,
            ForeColor = ThemeManager.TextMain,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 5, 0, 0)
        };

        _resetButton = new ModernButton
        {
            Text = "↺ Reset",
            Dock = DockStyle.Right,
            Width = 70,
            Height = 35,
            BackColor = Color.FromArgb(60, 60, 65),
            Font = new Font("Segoe UI", 8f)
        };
        _resetButton.Click += (s, e) => {
            _currentService.Reset();
            _chatHistoryContainer.Controls.Clear();
            _reasoningBox.Clear();
            _promptLogBox.Clear();
            AddMessage("System", "Chat history and session context cleared. Persistent history file wiped.");
        };

        headerPanel.Controls.Add(header);
        headerPanel.Controls.Add(_resetButton);

        _contentTabs = new TabControl { Dock = DockStyle.Fill };
        
        var chatPage = new TabPage("Chat") { BackColor = ThemeManager.Background };
        _chatHistoryContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        // Ensure child controls resize with the panel width
        _chatHistoryContainer.Resize += (s, e) => {
            foreach (Control ctrl in _chatHistoryContainer.Controls)
                ctrl.Width = _chatHistoryContainer.Width - 30; // Match AddMessage logic
        };
        chatPage.Controls.Add(_chatHistoryContainer);

        var thinkPage = new TabPage("Thinking 🧠") { BackColor = ThemeManager.Background };
        _reasoningBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 25),
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        thinkPage.Controls.Add(_reasoningBox);

        _contentTabs.TabPages.Add(chatPage);
        _contentTabs.TabPages.Add(thinkPage);

        var promptPage = new TabPage("Prompts 📜") { BackColor = ThemeManager.Background };
        _promptLogBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 25),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None
        };
        _promptLogBox.Text = "Full prompt logs will appear here when you send a message.";
        promptPage.Controls.Add(_promptLogBox);
        _contentTabs.TabPages.Add(promptPage);

        var bottomContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 170
        };

        _promptInput = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 100,
            Multiline = true,
            BackColor = ThemeManager.Surface,
            ForeColor = ThemeManager.TextMain,
            Font = ThemeManager.TextFont,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(5)
        };

        _timerLabel = new Label
        {
            Text = "Tokens: 0 | 0.0s",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = ThemeManager.TextSecondary,
            Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleRight
        };

        _sendButton = new ModernButton
        {
            Text = "Send Message",
            Dock = DockStyle.Bottom,
            Height = 40
        };
        _sendButton.Click += async (s, e) => await OnSendClick();

        bottomContainer.Controls.Add(_promptInput);
        bottomContainer.Controls.Add(_timerLabel);
        bottomContainer.Controls.Add(_sendButton);

        Controls.Add(_contentTabs);
        Controls.Add(headerPanel);
        Controls.Add(bottomContainer);
    }

    public async void PerformAction(string action)
    {
        string prompt = action switch
        {
            "build" => "Identify the build system and execute ONLY the 'build' command. IMPORTANT: Do NOT run, launch, or execute the project. Exit the terminal task immediately after the build binary is produced. DO NOT use 'dotnet watch' or 'dotnet run'—use only 'dotnet build' or equivalent.",
            "run" => "Please identify how to run the current project and execute the entry point in the terminal.",
            "test" => "Please find the unit tests for this project and run them in the terminal.",
            _ => ""
        };

        if (!string.IsNullOrEmpty(prompt))
        {
            AddMessage("User", $"[Toolbar Action: {action.ToUpper()}]");
            await ProcessChatAsync(prompt);
        }
    }

    public void ResetUI()
    {
        _chatHistoryContainer.Controls.Clear();
        foreach (var msg in _currentService.History)
        {
            AddMessage(msg.role == "user" ? "User" : "AI", msg.content);
        }
        
        if (_currentService.History.Count == 0)
        {
            AddMessage("System", $"Workspace loaded: {_currentService.WorkingDirectory}");
        }
    }

    private ChatMessageControl AddMessage(string sender, string message)
    {
        var msg = new ChatMessageControl(sender, message)
        {
            Width = _chatHistoryContainer.Width - 30 // Account for scrollbar
        };
        msg.OnApplyCode += (code) => OnApplyCodeRequested?.Invoke(code);
        _chatHistoryContainer.Controls.Add(msg);
        _chatHistoryContainer.ScrollControlIntoView(msg);
        return msg;
    }

    private async Task OnSendClick()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            AddMessage("System", "🛑 Task cancellation requested by user.");
            return;
        }

        string userPrompt = _promptInput.Text.Trim();
        if (string.IsNullOrEmpty(userPrompt)) return;

        AddMessage("User", userPrompt);
        _promptInput.Clear();
        await ProcessChatAsync(userPrompt);
    }

    private async Task ProcessChatAsync(string userPrompt)
    {
        _cts = new System.Threading.CancellationTokenSource();
        _sendButton.Text = "Stop Task";
        _sendButton.BackColor = Color.Maroon;
        _promptInput.Enabled = false;
        
        _timerLabel.Text = "Generating...";
        _reasoningBox.Clear();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _sendButton.Text = "AI Thinking...";
            _sendButton.Update();

            // SINGLE TURN: No loop. One request, executable tools, then done.
            var response = await _currentService.ChatAsync(userPrompt, _settingsService.Current, addToHistory: true, leanContext: false, _cts.Token);
            
            var thinkMatch = System.Text.RegularExpressions.Regex.Match(response, @"<think>(.*?)</think>", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (thinkMatch.Success)
                _reasoningBox.Text = thinkMatch.Groups[1].Value.Trim();

            string displayMsg = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
            AddMessage("AI", displayMsg);

            var tools = ToolParser.Parse(response);
            foreach (var tool in tools)
            {
                _cts.Token.ThrowIfCancellationRequested();
                _sendButton.Text = $"🔧 {tool.Action}...";
                _sendButton.Update();
                _timerLabel.Text = $"🔧 {tool.Action}...";
                
                string result = await HandleToolCall(tool, _cts.Token);
                AddMessage("System", $"🔧 Tool [{tool.Action}]: {result}");
            }

            // PERMANENT COMMIT: Clean the intermediate history if any, then save the turn.
            // Since it's a single turn now, we just save what we have.
            _currentService.SaveHistory();
            
            sw.Stop();
            _timerLabel.Text = $"Action Complete | {sw.Elapsed.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _timerLabel.Text = "Task Stopped by User";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _timerLabel.Text = "Error occurred";
            AddMessage("System", $"Error: {ex.Message}");
        }
        finally
        {
            ResetUIState();
        }
    }

    private void ResetUIState()
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke(new Action(ResetUIState));
            return;
        }

        _cts?.Dispose();
        _cts = null;
        _sendButton.Text = "Send Message";
        _sendButton.BackColor = ThemeManager.Primary;
        _sendButton.Enabled = true;
        _sendButton.Update(); // Force redraw
        _promptInput.Enabled = true;
        _promptInput.Focus();
    }

    private async Task<string> HandleToolCall(ToolCall tool, System.Threading.CancellationToken ct)
    {
        try
        {
            switch (tool.Action.ToLower())
            {
                case "read_file":
                    string readPath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    return File.Exists(readPath) ? File.ReadAllText(readPath) : $"Error: File '{readPath}' not found.";

                case "write_file":
                    string writePath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    string content = tool.Parameters.GetValueOrDefault("content")?.ToString() ?? "";
                    
                    // PROACTIVE: Ensure the target directory exists
                    string? dir = Path.GetDirectoryName(writePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(writePath, content);
                    _currentService.RefreshProjectMap();
                    OnFileModified?.Invoke(writePath);
                    return $"Success: Wrote to {writePath}";

                case "surgical_edit":
                    string editPath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    string search = tool.Parameters.GetValueOrDefault("search")?.ToString() ?? "";
                    string replace = tool.Parameters.GetValueOrDefault("replace")?.ToString() ?? "";
                    var editResult = SurgicalEditor.ReplaceContent(editPath, search, replace);
                    _currentService.RefreshProjectMap();
                    if (editResult.StartsWith("Success")) OnFileModified?.Invoke(editPath);
                    return editResult;

                case "run_command":
                    string command = tool.Parameters.GetValueOrDefault("command")?.ToString() ?? "";
                    var cmdResult = await _terminal.RunCommandAndCapture(command, 5000, ct); 
                    _currentService.RefreshProjectMap();
                    return cmdResult;

                case "kill_port":
                    string portStr = tool.Parameters.GetValueOrDefault("port")?.ToString() ?? "";
                    if (string.IsNullOrEmpty(portStr)) return "Error: No port specified.";
                    
                    // Use PowerShell to find the PID of the process on that port and stop it
                    string killCmd = $"Get-NetTCPConnection -LocalPort {portStr} -ErrorAction SilentlyContinue | " +
                                     "Select-Object -ExpandProperty OwningProcess -First 1 | " +
                                     "ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }";
                    
                    await _terminal.RunCommandAndCapture(killCmd, 5000, ct);
                    return $"Success: Attempted to terminate process on port {portStr}.";

                case "list_directory":
                    string dirPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? ".";
                    // If dirPath is relative, join with WorkingDirectory
                    if (!Path.IsPathRooted(dirPath) && !string.IsNullOrEmpty(_currentService.WorkingDirectory))
                        dirPath = Path.Combine(_currentService.WorkingDirectory, dirPath);

                    if (!Directory.Exists(dirPath)) return $"Error: Directory '{dirPath}' not found.";
                    var info = new DirectoryInfo(dirPath);
                    var list = info.GetFileSystemInfos().Select(f => (f.Attributes.HasFlag(FileAttributes.Directory) ? "[DIR] " : "[FILE] ") + f.Name);
                    return string.Join("\n", list);

                default:
                    return $"Error: Unknown tool {tool.Action}";
            }
        }
        catch (OperationCanceledException)
        {
            return "Task cancelled by user.";
        }
        catch (Exception ex)
        {
            return $"Error executing tool: {ex.Message}";
        }
    }

    private string ResolveFinalPath(string? rawPath)
    {
        // 1. If empty, use ActiveFilePath
        if (string.IsNullOrEmpty(rawPath))
            return _currentService.ActiveFilePath ?? "";

        // 2. If already absolute and exists, use it
        if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            return rawPath;

        // 3. Match against ActiveFilePath filename (if AI says "Home.razor" and it's open)
        if (!string.IsNullOrEmpty(_currentService.ActiveFilePath))
        {
            try
            {
                string activeFileName = Path.GetFileName(_currentService.ActiveFilePath);
                if (rawPath.Equals(activeFileName, StringComparison.OrdinalIgnoreCase) ||
                    rawPath.EndsWith("\\" + activeFileName, StringComparison.OrdinalIgnoreCase) ||
                    rawPath.EndsWith("/" + activeFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return _currentService.ActiveFilePath;
                }
            }
            catch { }
        }

        // 4. Try joining with WorkingDirectory
        if (!string.IsNullOrEmpty(_currentService.WorkingDirectory))
        {
            string combined = Path.Combine(_currentService.WorkingDirectory, rawPath);
            if (File.Exists(combined)) return combined;

            // Handle sub-project paths (e.g. AI says "BlazorHello/Pages/Home.razor" but we are IN "BlazorHello")
            string? currentDir = _currentService.WorkingDirectory;
            while (currentDir != null)
            {
                string alternative = Path.Combine(currentDir, rawPath);
                if (File.Exists(alternative)) return alternative;
                currentDir = Path.GetDirectoryName(currentDir);
            }
        }

        // 5. HEURISTIC FOR NEW FILES: If "Pages/Animals.razor" is new, search for an existing "Pages" folder in subdirectories
        if (!Path.IsPathRooted(rawPath) && !string.IsNullOrEmpty(_currentService.WorkingDirectory))
        {
            try
            {
                string? searchDir = Path.GetDirectoryName(rawPath.Replace('/', '\\'));
                if (!string.IsNullOrEmpty(searchDir))
                {
                    // Search for a matching leaf directory in the project tree
                    var matches = Directory.GetDirectories(_currentService.WorkingDirectory, searchDir, SearchOption.AllDirectories)
                                           .Where(d => !d.Contains("\\bin\\") && !d.Contains("\\obj\\") && !d.Contains("\\.git\\"))
                                           .ToList();
                    
                    if (matches.Count == 1)
                    {
                        return Path.GetFullPath(Path.Combine(matches[0], Path.GetFileName(rawPath)));
                    }
                    else if (matches.Count > 1 && !string.IsNullOrEmpty(_currentService.ActiveFilePath))
                    {
                        // Multiple matches? Pick the one that shares a parent structure with the Active File
                        string activeDir = Path.GetDirectoryName(_currentService.ActiveFilePath) ?? "";
                        var bestMatch = matches.OrderBy(m => GetSharedPathLength(m, activeDir)).Last();
                        return Path.GetFullPath(Path.Combine(bestMatch, Path.GetFileName(rawPath)));
                    }
                }
            }
            catch { }
        }

        // 6. FINAL FALLBACK: If not absolute and WorkingDirectory exists, root it there
        if (!Path.IsPathRooted(rawPath) && !string.IsNullOrEmpty(_currentService.WorkingDirectory))
            return Path.GetFullPath(Path.Combine(_currentService.WorkingDirectory, rawPath));

        return rawPath;
    }

    private static int GetSharedPathLength(string p1, string p2)
    {
        var parts1 = p1.Split(Path.DirectorySeparatorChar);
        var parts2 = p2.Split(Path.DirectorySeparatorChar);
        int length = 0;
        for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
        {
            if (parts1[i].Equals(parts2[i], StringComparison.OrdinalIgnoreCase)) length++;
            else break;
        }
        return length;
    }
}
