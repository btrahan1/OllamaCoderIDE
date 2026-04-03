using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OllamaCoderIDE.Services;

namespace OllamaCoderIDE.Controls;

public class ChatPanelControl : BaseStyledControl
{
    private readonly OllamaService _ollama;
    private readonly SettingsService _settingsService;
    private readonly TerminalControl _terminal;
    private TabControl _contentTabs = null!;
    private Panel _chatHistoryContainer = null!;
    private RichTextBox _reasoningBox = null!;
    private TextBox _promptInput = null!;
    private ModernButton _sendButton = null!;
    private Label _timerLabel = null!;
    private ModernButton _resetButton = null!;
    public event Action<string>? OnApplyCodeRequested;

    public ChatPanelControl(OllamaService ollama, SettingsService settingsService, TerminalControl terminal)
    {
        _ollama = ollama;
        _settingsService = settingsService;
        _terminal = terminal;
        Dock = DockStyle.Fill;
        Padding = new Padding(15);
        InitializeChat();
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
            _ollama.Reset();
            _chatHistoryContainer.Controls.Clear();
            _reasoningBox.Clear();
            AddMessage("System", "Chat history and session context cleared.");
        };

        headerPanel.Controls.Add(header);
        headerPanel.Controls.Add(_resetButton);

        _contentTabs = new TabControl { Dock = DockStyle.Fill };
        
        var chatPage = new TabPage("Chat") { BackColor = ThemeManager.Background };
        _chatHistoryContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(10)
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

        var bottomContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 130
        };

        _promptInput = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 60,
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
        foreach (var msg in _ollama.History)
        {
            AddMessage(msg.role == "user" ? "User" : "AI", msg.content);
        }
        
        if (_ollama.History.Count == 0)
        {
            AddMessage("System", $"Workspace loaded: {_ollama.WorkingDirectory}");
        }
    }

    private ChatMessageControl AddMessage(string sender, string message)
    {
        var msg = new ChatMessageControl(sender, message);
        msg.OnApplyCode += (code) => OnApplyCodeRequested?.Invoke(code);
        _chatHistoryContainer.Controls.Add(msg);
        msg.SendToBack(); // Force to bottom of Dock=Top stack
        _chatHistoryContainer.ScrollControlIntoView(msg);
        return msg;
    }

    private async Task OnSendClick()
    {
        string userPrompt = _promptInput.Text.Trim();
        if (string.IsNullOrEmpty(userPrompt)) return;

        AddMessage("User", userPrompt);
        _promptInput.Clear();
        await ProcessChatAsync(userPrompt);
    }

    private async Task ProcessChatAsync(string userPrompt)
    {
        _sendButton.Enabled = false;
        _timerLabel.Text = "Generating...";
        _reasoningBox.Clear();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string currentPrompt = userPrompt;
            bool processing = true;
            int loopCount = 0;

            while (processing && loopCount < 5)
            {
                loopCount++;
                var response = await _ollama.ChatAsync(currentPrompt, _settingsService.Current);
                
                // Extract thinking if present for the thinking tab
                var thinkMatch = System.Text.RegularExpressions.Regex.Match(response, @"<think>(.*?)</think>", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (thinkMatch.Success)
                    _reasoningBox.Text = thinkMatch.Groups[1].Value.Trim();

                // Remove think tags for the main chat
                string displayMsg = System.Text.RegularExpressions.Regex.Replace(response, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
                
                AddMessage("AI", displayMsg);

                // Parse for tool calls
                var tools = ToolParser.Parse(response);
                if (tools.Count > 0)
                {
                    var toolResults = new List<string>();
                    foreach (var tool in tools)
                    {
                        _timerLabel.Text = $"🔧 {tool.Action}...";
                        string result = await HandleToolCall(tool);
                        toolResults.Add(result);
                        AddMessage("System", $"🔧 Tool [{tool.Action}]: {result}");
                    }
                    currentPrompt = "TOOL RESULTS:\n" + string.Join("\n", toolResults);
                    processing = true;
                }
                else
                {
                    processing = false;
                }
            }
            
            sw.Stop();
            _timerLabel.Text = $"Agent Loop Complete | {sw.Elapsed.TotalSeconds:F1}s";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _timerLabel.Text = "Error occurred";
            AddMessage("System", $"Error: {ex.Message}");
        }
        finally
        {
            _sendButton.Enabled = true;
        }
    }

    private async Task<string> HandleToolCall(ToolCall tool)
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
                    File.WriteAllText(writePath, content);
                    _ollama.RefreshProjectMap();
                    return $"Success: Wrote to {writePath}";

                case "surgical_edit":
                    string editPath = ResolveFinalPath(tool.Parameters.GetValueOrDefault("path")?.ToString());
                    string search = tool.Parameters.GetValueOrDefault("search")?.ToString() ?? "";
                    string replace = tool.Parameters.GetValueOrDefault("replace")?.ToString() ?? "";
                    var editResult = SurgicalEditor.ReplaceContent(editPath, search, replace);
                    _ollama.RefreshProjectMap();
                    return editResult;

                case "run_command":
                    string command = tool.Parameters.GetValueOrDefault("command")?.ToString() ?? "";
                    var cmdResult = await _terminal.RunCommandAndCapture(command);
                    _ollama.RefreshProjectMap();
                    return cmdResult;

                case "kill_port":
                    string portStr = tool.Parameters.GetValueOrDefault("port")?.ToString() ?? "";
                    if (string.IsNullOrEmpty(portStr)) return "Error: No port specified.";
                    
                    // Use PowerShell to find the PID of the process on that port and stop it
                    string killCmd = $"Get-NetTCPConnection -LocalPort {portStr} -ErrorAction SilentlyContinue | " +
                                     "Select-Object -ExpandProperty OwningProcess -First 1 | " +
                                     "ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }";
                    
                    await _terminal.RunCommandAndCapture(killCmd);
                    return $"Success: Attempted to terminate process on port {portStr}.";

                case "list_directory":
                    string dirPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? ".";
                    // If dirPath is relative, join with WorkingDirectory
                    if (!Path.IsPathRooted(dirPath) && !string.IsNullOrEmpty(_ollama.WorkingDirectory))
                        dirPath = Path.Combine(_ollama.WorkingDirectory, dirPath);

                    if (!Directory.Exists(dirPath)) return $"Error: Directory '{dirPath}' not found.";
                    var info = new DirectoryInfo(dirPath);
                    var list = info.GetFileSystemInfos().Select(f => (f.Attributes.HasFlag(FileAttributes.Directory) ? "[DIR] " : "[FILE] ") + f.Name);
                    return string.Join("\n", list);

                default:
                    return $"Error: Unknown tool {tool.Action}";
            }
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
            return _ollama.ActiveFilePath ?? "";

        // 2. If already absolute and exists, use it
        if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            return rawPath;

        // 3. Match against ActiveFilePath filename (if AI says "Home.razor" and it's open)
        if (!string.IsNullOrEmpty(_ollama.ActiveFilePath))
        {
            try
            {
                string activeFileName = Path.GetFileName(_ollama.ActiveFilePath);
                if (rawPath.Equals(activeFileName, StringComparison.OrdinalIgnoreCase) ||
                    rawPath.EndsWith("\\" + activeFileName, StringComparison.OrdinalIgnoreCase) ||
                    rawPath.EndsWith("/" + activeFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return _ollama.ActiveFilePath;
                }
            }
            catch { }
        }

        // 4. Try joining with WorkingDirectory
        if (!string.IsNullOrEmpty(_ollama.WorkingDirectory))
        {
            string combined = Path.Combine(_ollama.WorkingDirectory, rawPath);
            if (File.Exists(combined)) return combined;

            // Handle sub-project paths (e.g. AI says "BlazorHello/Pages/Home.razor" but we are IN "BlazorHello")
            string? currentDir = _ollama.WorkingDirectory;
            while (currentDir != null)
            {
                string alternative = Path.Combine(currentDir, rawPath);
                if (File.Exists(alternative)) return alternative;
                currentDir = Path.GetDirectoryName(currentDir);
            }
        }

        return rawPath;
    }
}
