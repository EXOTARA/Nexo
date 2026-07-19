using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nexo.Core.Assistant;

namespace Nexo.App.Views;

public partial class AssistantView : UserControl
{
    private const string WelcomeText =
        "Hola. Puedo ejecutar órdenes locales y mostrar confirmaciones discretas. Prueba “muestra Peek”, “cómo está mi PC” o “abre PowerShell”.";

    private readonly List<ConversationMessage> _messages = [];
    private readonly StringBuilder _streamingBuffer = new();
    private Border? _streamingBubble;
    private TextBlock? _streamingTextBlock;
    private bool _streamingHasContent;
    private string _aiProviderStatus = "IA desactivada · los comandos locales siguen disponibles";
    private bool _saveHistory;
    private int _recentMessageLimit = 8;

    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;
    public event EventHandler? ConversationChanged;
    public event EventHandler? ConversationCleared;
    public event EventHandler? VoiceInputStarted;
    public event EventHandler? VoiceInputStopped;

    private bool _voiceInputActive;
    private bool _voiceAvailable;

    public AssistantView()
    {
        InitializeComponent();
        RenderConversation();
    }

    public void ConfigureHistory(bool saveHistory, int recentMessageLimit)
    {
        _saveHistory = saveHistory;
        _recentMessageLimit = Math.Clamp(recentMessageLimit, 4, 30);
        TrimTransientConversation();
        RenderConversation();
    }

    public void LoadConversation(IEnumerable<ConversationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        _messages.Clear();
        _messages.AddRange(messages.Where(message => !string.IsNullOrWhiteSpace(message.Text)));
        TrimTransientConversation();
        RenderConversation();
    }

    public IReadOnlyList<ConversationMessage> GetConversationSnapshot() => _messages.ToArray();

    public void FocusPrompt()
    {
        Dispatcher.BeginInvoke(() =>
        {
            PromptBox.Focus();
            Keyboard.Focus(PromptBox);
        }, DispatcherPriority.Input);
    }

    public void SetAiProviderStatus(string detail)
    {
        _aiProviderStatus = detail;
        if (AiProviderStatusText is not null)
        {
            AiProviderStatusText.Text = detail;
        }
    }

    public void SetAiActivity(string? activity)
    {
        if (AiProviderStatusText is null)
        {
            return;
        }

        AiProviderStatusText.Text = string.IsNullOrWhiteSpace(activity)
            ? _aiProviderStatus
            : $"{_aiProviderStatus} · {activity}";
    }

    public void SetVoiceAvailability(bool available, string detail)
    {
        _voiceAvailable = available;

        if (MicButton is null || VoiceStatusText is null)
        {
            return;
        }

        if (!_voiceInputActive)
        {
            MicButton.Content = "Mic";
            MicButton.IsEnabled = available;
            MicButton.ClearValue(BackgroundProperty);
        }

        VoiceStatusText.Text = detail;
    }

    public void SetVoiceState(AssistantVoiceState state, string? detail = null)
    {
        if (MicButton is null || VoiceStatusText is null)
        {
            return;
        }

        switch (state)
        {
            case AssistantVoiceState.Listening:
                MicButton.Content = "Suelta";
                MicButton.IsEnabled = true;
                MicButton.Background = (Brush)FindResource("BrushAccentSoft");
                VoiceStatusText.Text = detail ?? "Escuchando… suelta Mic cuando termines.";
                break;

            case AssistantVoiceState.Processing:
                MicButton.Content = "…";
                MicButton.IsEnabled = false;
                VoiceStatusText.Text = detail ?? "Convirtiendo tu voz en una orden…";
                break;

            case AssistantVoiceState.Error:
                MicButton.Content = "Mic";
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ?? "No pude usar el micrófono.";
                break;

            default:
                MicButton.Content = "Mic";
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ??
                    "Voz local lista. Mantén Mic presionado mientras hablas.";
                break;
        }
    }

    public void BeginNexoStreamingMessage(string placeholder = "Pensando…")
    {
        CancelNexoStreamingMessage();

        _streamingBuffer.Clear();
        _streamingHasContent = false;
        _streamingBubble = CreateMessageBubble(
            placeholder,
            HorizontalAlignment.Left,
            (Brush)FindResource("BrushSurfaceRaised"));
        _streamingTextBlock = _streamingBubble.Child as TextBlock;

        ConversationPanel.Children.Add(_streamingBubble);
        ScrollConversationToEnd();
    }

    public void AppendNexoStreamingText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_streamingBubble is null || _streamingTextBlock is null)
        {
            BeginNexoStreamingMessage();
        }

        if (!_streamingHasContent)
        {
            _streamingBuffer.Clear();
            _streamingTextBlock!.Text = string.Empty;
            _streamingHasContent = true;
        }

        _streamingBuffer.Append(text);
        _streamingTextBlock!.Text = _streamingBuffer.ToString();
        ScrollConversationToEnd();
    }

    public string CompleteNexoStreamingMessage()
    {
        var text = _streamingBuffer.ToString().Trim();
        ClearStreamingReferences(removeBubble: false);

        if (string.IsNullOrWhiteSpace(text))
        {
            RenderConversation();
            return string.Empty;
        }

        _messages.Add(new ConversationMessage(
            ConversationRole.Assistant,
            text,
            DateTimeOffset.Now));
        TrimTransientConversation();
        RenderConversation();
        ConversationChanged?.Invoke(this, EventArgs.Empty);
        return text;
    }

    public void CancelNexoStreamingMessage()
    {
        ClearStreamingReferences(removeBubble: true);
    }

    public void AddUserMessage(string text)
    {
        AddMessage(new ConversationMessage(
            ConversationRole.User,
            text,
            DateTimeOffset.Now));
    }

    public void AddNexoMessage(string text)
    {
        AddMessage(new ConversationMessage(
            ConversationRole.Assistant,
            text,
            DateTimeOffset.Now));
    }

    private void AddMessage(ConversationMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        _messages.Add(message);
        TrimTransientConversation();
        RenderConversation();
        ConversationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimTransientConversation()
    {
        if (_saveHistory || _messages.Count <= _recentMessageLimit)
        {
            return;
        }

        _messages.RemoveRange(0, _messages.Count - _recentMessageLimit);
    }

    private void RenderConversation()
    {
        if (ConversationPanel is null)
        {
            return;
        }

        ClearStreamingReferences(removeBubble: false);
        ConversationPanel.Children.Clear();

        if (_messages.Count == 0)
        {
            ConversationPanel.Children.Add(CreateMessageBubble(
                WelcomeText,
                HorizontalAlignment.Left,
                (Brush)FindResource("BrushSurfaceRaised")));
        }
        else
        {
            foreach (var message in _messages)
            {
                var isUser = message.Role == ConversationRole.User;
                ConversationPanel.Children.Add(CreateMessageBubble(
                    message.Text,
                    isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    (Brush)FindResource(isUser ? "BrushAccentSoft" : "BrushSurfaceRaised")));
            }
        }

        ScrollConversationToEnd();
    }

    private void ScrollConversationToEnd()
    {
        Dispatcher.BeginInvoke(
            new Action(ConversationScroll.ScrollToEnd),
            DispatcherPriority.Background);
    }

    private void ClearStreamingReferences(bool removeBubble)
    {
        if (removeBubble && _streamingBubble is not null && ConversationPanel is not null)
        {
            ConversationPanel.Children.Remove(_streamingBubble);
        }

        _streamingBubble = null;
        _streamingTextBlock = null;
        _streamingBuffer.Clear();
        _streamingHasContent = false;
    }

    private void MicButton_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        BeginVoiceInput();
        e.Handled = true;
    }

    private void MicButton_PreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        EndVoiceInput();
        e.Handled = true;
    }

    private void MicButton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        EndVoiceInput();
    }

    private void MicButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            BeginVoiceInput();
            e.Handled = true;
        }
    }

    private void MicButton_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            EndVoiceInput();
            e.Handled = true;
        }
    }

    private void BeginVoiceInput()
    {
        if (_voiceInputActive || MicButton.IsEnabled == false)
        {
            return;
        }

        _voiceInputActive = true;
        MicButton.CaptureMouse();
        VoiceInputStarted?.Invoke(this, EventArgs.Empty);
    }

    private void EndVoiceInput()
    {
        if (!_voiceInputActive)
        {
            return;
        }

        _voiceInputActive = false;
        if (MicButton.IsMouseCaptured)
        {
            MicButton.ReleaseMouseCapture();
        }

        VoiceInputStopped?.Invoke(this, EventArgs.Empty);
    }

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            SubmitPrompt();
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitPrompt();
    }

    private void ClearConversationButton_Click(object sender, RoutedEventArgs e)
    {
        CancelNexoStreamingMessage();
        _messages.Clear();
        RenderConversation();
        ConversationCleared?.Invoke(this, EventArgs.Empty);
        FocusPrompt();
    }

    private void SubmitPrompt()
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PromptBox.Clear();
        PromptSubmitted?.Invoke(this, new PromptSubmittedEventArgs(prompt));
        FocusPrompt();
    }

    private static Border CreateMessageBubble(
        string text,
        HorizontalAlignment alignment,
        Brush background)
    {
        return new Border
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(13),
            Background = background,
            HorizontalAlignment = alignment,
            MaxWidth = 310,
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.FindResource("BrushTextPrimary")
            }
        };
    }
}

public enum AssistantVoiceState
{
    Idle,
    Listening,
    Processing,
    Error
}

public sealed class PromptSubmittedEventArgs : EventArgs
{
    public PromptSubmittedEventArgs(string prompt)
    {
        Prompt = prompt;
    }

    public string Prompt { get; }
}
