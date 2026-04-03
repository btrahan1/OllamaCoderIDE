using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using OllamaCoderIDE.Services;

namespace OllamaCoderIDE.Controls;

public class ChatPanelControl : BaseStyledControl
{
    private readonly OllamaService _ollama;
    private readonly SettingsService _settingsService;
    private Panel _chatHistoryContainer = null!;
    private TextBox _promptInput = null!;
    private ModernButton _sendButton = null!;
    private Label _timerLabel = null!;
    private ModernButton _resetButton = null!;
    public event Action<string>? OnApplyCodeRequested;

    public ChatPanelControl(OllamaService ollama, SettingsService settingsService)
    {
        _ollama = ollama;
        _settingsService = settingsService;
        Dock = DockStyle.Right;
        Width = 350;
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
            AddMessage("System", "Chat history and session context cleared.");
        };

        headerPanel.Controls.Add(header);
        headerPanel.Controls.Add(_resetButton);

        _chatHistoryContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = ThemeManager.Surface,
            Padding = new Padding(10)
        };

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
            Text = "Response Time: 0.0s",
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

        Controls.Add(_chatHistoryContainer);
        Controls.Add(headerPanel);
        Controls.Add(bottomContainer);
    }

    private void AddMessage(string sender, string message)
    {
        var msg = new ChatMessageControl(sender, message);
        msg.OnApplyCode += (code) => OnApplyCodeRequested?.Invoke(code);
        _chatHistoryContainer.Controls.Add(msg);
        _chatHistoryContainer.ScrollControlIntoView(msg);
    }

    private async Task OnSendClick()
    {
        string userPrompt = _promptInput.Text.Trim();
        if (string.IsNullOrEmpty(userPrompt)) return;

        AddMessage("User", userPrompt);
        _promptInput.Clear();
        _sendButton.Enabled = false;
        _timerLabel.Text = "Generating...";

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
                AddMessage("AI", response);

                // Parse for tool calls
                var tools = ToolParser.Parse(response);
                if (tools.Count > 0)
                {
                    var toolResults = new List<string>();
                    foreach (var tool in tools)
                    {
                        string result = await HandleToolCall(tool);
                        toolResults.Add(result);
                        AddMessage("System", $"🔧 Tool [{tool.Action}]: {result}");
                    }
                    // For the loop: feed the tool results back as a system-like observation
                    currentPrompt = "TOOL RESULTS:\n" + string.Join("\n", toolResults);
                }
                else
                {
                    processing = false;
                }
            }
            
            sw.Stop();
            _timerLabel.Text = $"Agent Loop Complete: {sw.Elapsed.TotalSeconds:F1}s";
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
                    string readPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? "";
                    return File.Exists(readPath) ? File.ReadAllText(readPath) : "Error: File not found.";

                case "write_file":
                    string writePath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? "";
                    string content = tool.Parameters.GetValueOrDefault("content")?.ToString() ?? "";
                    File.WriteAllText(writePath, content);
                    return $"Success: Wrote to {writePath}";

                case "surgical_edit":
                    string editPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? "";
                    string search = tool.Parameters.GetValueOrDefault("search")?.ToString() ?? "";
                    string replace = tool.Parameters.GetValueOrDefault("replace")?.ToString() ?? "";
                    return SurgicalEditor.ReplaceContent(editPath, search, replace);

                case "list_directory":
                    string dirPath = tool.Parameters.GetValueOrDefault("path")?.ToString() ?? ".";
                    if (!Directory.Exists(dirPath)) return "Error: Directory not found.";
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
}
