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
using System.Text.Json;

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
    private CheckBox _planningModeCheck = null!;
    private PlanControl? _planControl;
    private readonly SearchService _searchService;
    private readonly SessionLogService _sessionLogger = new();

    public event Action<string>? OnApplyCodeRequested;
    public event Action<string>? OnFileModified;

    public ChatPanelControl(ILLMService service, SettingsService settingsService, TerminalControl terminal, SearchService searchService, PlanControl? planControl = null)
    {
        _currentService = service;
        _settingsService = settingsService;
        _terminal = terminal;
        _searchService = searchService;
        _planControl = planControl;
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

    public void RefreshSystemPrompt()
    {
        AddMessage("System", $"🔄 Project Mode switched to: **{_settingsService.Current.ProjectType}**");
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
            Height = 195
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

        _planningModeCheck = new CheckBox
        {
            Text = "Planning Mode (No Execution)",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = ThemeManager.TextMain,
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            FlatStyle = FlatStyle.Flat
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
        bottomContainer.Controls.Add(_planningModeCheck);
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
            var chatMsg = AddMessage(msg.role == "user" ? "User" : "AI", msg.content);
            if (msg.role != "user") chatMsg.MarkAsComplete();
        }
        
        if (_currentService.History.Count == 0)
        {
            AddMessage("System", $"Workspace loaded: {_currentService.WorkingDirectory}");
        }
    }

    private ChatMessageControl AddMessage(string sender, string message)
    {
        if (this.InvokeRequired)
        {
            return (ChatMessageControl)this.Invoke(new Func<ChatMessageControl>(() => AddMessage(sender, message)));
        }

        var msg = new ChatMessageControl(sender, message)
        {
            Width = _chatHistoryContainer.Width - 30 // Account for scrollbar
        };
        msg.OnApplyCode += (code) => OnApplyCodeRequested?.Invoke(code);
        msg.OnExecuteTool += async (tool) => {
            AddMessage("System", $"🔧 Manually Triggering Tool [{tool.Action}]...");
            try {
                string result = await HandleToolCall(tool, System.Threading.CancellationToken.None);
                AddMessage("System", $"✅ Manual Tool [{tool.Action}] Result: {result}");
            } catch (Exception ex) {
                AddMessage("System", $"❌ Manual Tool [{tool.Action}] Failed: {ex.Message}");
            }
        };
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

    public async Task ProcessChatAsync(string userPrompt)
    {
        _cts = new System.Threading.CancellationTokenSource();
        _sendButton.Text = "Stop Task";
        _sendButton.BackColor = Color.Maroon;
        _promptInput.Enabled = false;
        
        _timerLabel.Text = "Generating...";
        _reasoningBox.Clear();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Action<string>? onPromptSent = null;

        // Move the core task logic to a background thread to keep UI responsive
        await Task.Run(async () => {
            try
            {
            if (!string.IsNullOrEmpty(_currentService.WorkingDirectory))
            {
                _sessionLogger.StartSession(_currentService.WorkingDirectory, _settingsService.Current.Provider.ToString());
            }

            string currentPrompt = "[SYSTEM: Turn counter reset to 0/30. You may begin the next phase of the task.]\n\n" + userPrompt;
            bool isPlanning = _planningModeCheck.Checked;
            int maxTurns = 50;
            int turn = 0;

            // Subscribe to raw prompt logging
            onPromptSent = (raw) => _sessionLogger.LogRequest(raw);
            _currentService.OnPromptSent += onPromptSent;

            while (turn < maxTurns)
            {
                turn++;
                _cts.Token.ThrowIfCancellationRequested();
#if DEBUG
                Console.WriteLine($"Turn {turn} starting...");
#endif

                this.Invoke(() => {
                    _sendButton.Text = turn == 1 ? "AI Thinking..." : $"AI Turn {turn}...";
                    _sendButton.Update();
                    _timerLabel.Text = turn == 1 ? "Generating..." : $"Autonomous Turn {turn}/{maxTurns}";
                });

                string finalPrompt = currentPrompt;
                if (isPlanning && turn == 1)
                {
                    finalPrompt = "[SYSTEM: YOU ARE IN PLANNING MODE. DO NOT PERFORM ANY TOOL CALLS. " + 
                                  "PROVIDE A DETAILED ARCHITECTURAL PLAN. YOU MUST WRAP YOUR STEP-BY-STEP " + 
                                  "LIST IN <plan>...</plan> TAGS (use - [ ] for each task) SO THE IDE CAN PARSE IT.]\n\n" + userPrompt;
                }

                // Get the composed system prompt based on project type
                string composedPrompt = _settingsService.GetFullSystemPrompt();

                // Call the AI service
                var response = await _currentService.ChatAsync(finalPrompt, _settingsService.Current, addToHistory: true, leanContext: false, systemPromptOverride: composedPrompt, ct: _cts.Token);

                _sessionLogger.LogResponse(response);
                
                // 1. Handle Thinking
                var thinkMatch = System.Text.RegularExpressions.Regex.Match(response, @"<think>(.*?)</think>", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (thinkMatch.Success)
                    this.Invoke(() => _reasoningBox.AppendText($"[TURN {turn}]\n{thinkMatch.Groups[1].Value.Trim()}\n\n"));

                // 2. Handle Visible Message
                string displayMsg = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
                if (!string.IsNullOrWhiteSpace(displayMsg))
                {
                    AddMessage("AI", displayMsg);
                }

                // 3. Detect and load Plans (Turn 1 only or if new plan appears)
                var planMatch = System.Text.RegularExpressions.Regex.Match(response, @"<plan>(.*?)</plan>", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (planMatch.Success && _planControl != null)
                {
                    this.Invoke(() => _planControl.LoadPlan(planMatch.Groups[1].Value.Trim()));
                    AddMessage("System", "📋 AI generated an Implementation Plan. Switching to Plan view.");
                }

                // 4. Handle Tools
                var parseResult = ToolParser.Parse(response);
                
                // VOCAL: Report any parsing failures immediately
                foreach (var err in parseResult.ParseErrors)
                {
                    AddMessage("System", $"❌ Tool Failure: {err}");
                }

                if (parseResult.Tools.Count == 0)
                {
                    // No tools? AI is finished.
                    break;
                }

                if (isPlanning)
                {
                    AddMessage("System", "ℹ️ AI proposed tools, but they were suppressed by Planning Mode.");
                    break;
                }

                var toolResults = new StringBuilder();
                foreach (var tool in parseResult.Tools)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    this.Invoke(() => {
                        _sendButton.Text = $"🔧 {tool.Action}...";
                        _sendButton.Update();
                        _timerLabel.Text = $"🔧 {tool.Action}...";
                    });

                    string result = await HandleToolCall(tool, _cts.Token);
                    _sessionLogger.LogTool(tool.Action, JsonSerializer.Serialize(tool.Parameters), result);
                    AddMessage("System", $"🔧 Tool [{tool.Action}]: {result}");
                    toolResults.AppendLine($"Tool [{tool.Action}] Result: {result}");
                }

                // Set up prompt for next turn
                currentPrompt = $"### TOOL RESULTS:\n{toolResults}\n\n### INSTRUCTION:\nContinue with the next steps of the task based on these results. If finished, provide a brief final summary.";
            }

            if (turn >= maxTurns)
            {
                AddMessage("System", "⚠️ Maximum autonomous turns (20) reached. Please prompt the AI to continue if the task is incomplete.");
            }

            _currentService.SaveHistory();
            
            sw.Stop();
            this.Invoke(() => _timerLabel.Text = $"Action Complete | {sw.Elapsed.TotalSeconds:F1}s");
            
            if (!string.IsNullOrEmpty(_sessionLogger.GetLogPath()))
            {
                AddMessage("System", $"📝 Full session log saved to: {_sessionLogger.GetLogPath()}");
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            this.Invoke(() => _timerLabel.Text = "Task Stopped by User");
        }
        catch (Exception ex)
        {
            sw.Stop();
            this.Invoke(() => _timerLabel.Text = "Error occurred");
            AddMessage("System", $"Error: {ex.Message}");
        }
        finally
        {
            if (onPromptSent != null)
                _currentService.OnPromptSent -= onPromptSent;
            ResetUIState();
        }
        });
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
                    string newContent = tool.Parameters.GetValueOrDefault("content")?.ToString() ?? "";
                    string oldContent = File.Exists(writePath) ? File.ReadAllText(writePath) : "";

                    _sessionLogger.LogDiagnostic($"Showing Diff Dialog for {writePath}...");
                    var writeResult = ShowDiff(writePath, oldContent, newContent);
                    _sessionLogger.LogDiagnostic($"Diff Dialog result for {writePath}: {writeResult}");
                    if (!writeResult) return "Error: Change was rejected by the user.";
                    
                    // PROACTIVE: Ensure the target directory exists
                    string? dir = Path.GetDirectoryName(writePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(writePath, newContent);
                    _currentService.RefreshProjectMap();
                    OnFileModified?.Invoke(writePath);
                    return $"Success: Wrote to {writePath}";

                case "surgical_edit":
                    string editPath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    string search = tool.Parameters.GetValueOrDefault("search")?.ToString() ?? "";
                    string replace = tool.Parameters.GetValueOrDefault("replace")?.ToString() ?? "";

                    if (!File.Exists(editPath)) return $"Error: File '{editPath}' not found.";

                    string currentSurgical = File.ReadAllText(editPath);
                    string updatedSurgical = SurgicalEditor.PreviewEdit(currentSurgical, search, replace);

                    if (updatedSurgical == currentSurgical) return "Error: Could not find the unique 'search' block in the file. Ensure you provide exact lines and enough context.";

                    // Always show Diff Dialog
                    var surgicalResult = ShowDiff(editPath, currentSurgical, updatedSurgical);
                    if (!surgicalResult) return "Error: Change was rejected by the user.";

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
                    string rawDirPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? ".";
                    string dirPath = ResolveFinalPath(rawDirPath);

                    if (!Directory.Exists(dirPath)) return $"Error: Directory '{dirPath}' not found.";
                    var info = new DirectoryInfo(dirPath);
                    var list = info.GetFileSystemInfos().Select(f => (f.Attributes.HasFlag(FileAttributes.Directory) ? "[DIR] " : "[FILE] ") + f.Name);
                    return string.Join("\n", list);

                case "grep_search":
                    string pattern = tool.Parameters.GetValueOrDefault("pattern")?.ToString() ?? "";
                    bool isRegex = tool.Parameters.GetValueOrDefault("is_regex")?.ToString()?.ToLower() == "true";
                    string root = _currentService.WorkingDirectory ?? AppContext.BaseDirectory;
                    return _searchService.GrepSearch(root, pattern, isRegex);

                case "get_symbols":
                    string symbolPath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    if (!File.Exists(symbolPath)) return "Error: File not found.";
                    return ParseSymbols(File.ReadAllText(symbolPath));

                case "git_status":
                    return await _terminal.RunCommandAndCapture("git status --short", 3000, ct);

                case "git_commit":
                    string msg = tool.Parameters.GetValueOrDefault("message")?.ToString() ?? "Update from AI";
                    await _terminal.RunCommandAndCapture("git add .", 3000, ct);
                    return await _terminal.RunCommandAndCapture($"git commit -m \"{msg}\"", 3000, ct);

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
        // 0. Handle Current Directory or Empty Path explicitly
        if (string.IsNullOrEmpty(rawPath) || rawPath == ".")
            return _currentService.WorkingDirectory ?? _currentService.ActiveFilePath ?? "";

        _sessionLogger.LogDiagnostic($"Resolving path: '{rawPath}'");
        
        // Normalize slashes
        rawPath = (rawPath ?? "").Replace('/', '\\');

        // 1. Direct Match (Absolute or Relative to WD)
        if (Path.IsPathRooted(rawPath))
        {
            if (File.Exists(rawPath) || Directory.Exists(rawPath)) return rawPath;
        }
        else if (!string.IsNullOrEmpty(_currentService.WorkingDirectory))
        {
            string combined = Path.Combine(_currentService.WorkingDirectory, rawPath);
            if (File.Exists(combined) || Directory.Exists(combined)) return Path.GetFullPath(combined);
        }

        // 2. Proactive Search (Recursive)
        if (!string.IsNullOrEmpty(_currentService.WorkingDirectory))
        {
            try
            {
                string targetName = Path.GetFileName(rawPath);
                if (!string.IsNullOrEmpty(targetName))
                {
                    _sessionLogger.LogDiagnostic($"Starting proactive search for '{targetName}' in '{_currentService.WorkingDirectory}'...");
                    var swSearch = System.Diagnostics.Stopwatch.StartNew();
                    var allPossible = Directory.GetFileSystemEntries(_currentService.WorkingDirectory, targetName, SearchOption.AllDirectories)
                                               .Where(e => IsAllowedPath(e, _currentService.WorkingDirectory))
                                               .ToList();
                    swSearch.Stop();
                    _sessionLogger.LogDiagnostic($"Proactive search finished in {swSearch.ElapsedMilliseconds}ms. Found {allPossible.Count} matches.");

                    if (allPossible.Any())
                    {
                        // If multiple matches, pick the one that matches the MOST of the original raw path suffix
                        var best = allPossible.OrderByDescending(e => {
                            string eLower = e.ToLower().Replace('/', '\\');
                            string rLower = rawPath.ToLower().Replace('/', '\\');
                            int score = 0;
                            if (eLower.EndsWith(rLower)) score += 1000;
                            return score + GetSharedPathLength(e, _currentService.ActiveFilePath ?? "");
                        }).First();

                        return Path.GetFullPath(best);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Path Resolution Search Error: {ex.Message}");
            }
        }

        // 3. Last Resort Fallback
        if (!Path.IsPathRooted(rawPath) && !string.IsNullOrEmpty(_currentService.WorkingDirectory))
            return Path.GetFullPath(Path.Combine(_currentService.WorkingDirectory, rawPath));

        return rawPath;
    }

    private bool IsAllowedPath(string path, string workingDirectory)
    {
        try
        {
            var relative = Path.GetRelativePath(workingDirectory, path).ToLower();
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !parts.Any(p => p == ".git" || p == ".vs" || p == "bin" || p == "obj" || p == ".ollama" || p == ".gemini_coder");
        }
        catch { return false; }
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

    private bool ShowDiff(string path, string oldContent, string newContent)
    {
        if (this.InvokeRequired)
        {
            return (bool)this.Invoke(new Func<bool>(() => ShowDiff(path, oldContent, newContent)));
        }

        using (var dlg = new DiffDialog(path, oldContent, newContent))
        {
            return dlg.ShowDialog() == DialogResult.OK;
        }
    }

    private string ParseSymbols(string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SYMBOLS FOUND:");
        
        // Very basic regex for C# symbols
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("public") || trimmed.StartsWith("private") || trimmed.StartsWith("internal") || trimmed.StartsWith("protected"))
            {
                if (trimmed.Contains("class ") || trimmed.Contains("interface ") || trimmed.Contains("enum ") || trimmed.Contains("("))
                {
                    // Clean up for compact view
                    string cleaned = System.Text.RegularExpressions.Regex.Replace(trimmed, @"{.*", "").Trim();
                    sb.AppendLine($"- {cleaned}");
                }
            }
        }
        return sb.ToString();
    }
}
