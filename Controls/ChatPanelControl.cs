using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
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
        var header = new Label
        {
            Text = "Ollama Chat",
            Dock = DockStyle.Top,
            Font = ThemeManager.HeaderFont,
            ForeColor = ThemeManager.TextMain,
            Height = 40
        };

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
        Controls.Add(header);
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
        string prompt = _promptInput.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        AddMessage("User", prompt);
        _promptInput.Clear();
        _sendButton.Enabled = false;
        _timerLabel.Text = "Generating...";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _ollama.GenerateAsync(prompt, _settingsService.Current);
            sw.Stop();
            _timerLabel.Text = $"Response Time: {sw.Elapsed.TotalSeconds:F1}s";
            AddMessage("AI", response);
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
}
