using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nexo.Core.Assistant;

namespace Nexo.App.Views;

public partial class AssistantView : UserControl
{
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
    public event EventHandler? VisionCaptureRequested;
    public event EventHandler? VisionAttachmentCleared;

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

    public bool HasVisionAttachment =>
        VisionAttachmentPanel is not null &&
        VisionAttachmentPanel.Visibility == Visibility.Visible;

    public void SetVisionAvailability(bool available)
    {
        if (VisionButton is not null)
        {
            VisionButton.IsEnabled = available;
            VisionButton.ToolTip = available
                ? "Mirar la ventana activa · Ctrl + Shift + Espacio"
                : "Kohana Vision está desactivado en Personalización";
        }
    }

    public void SetVisionAttachment(
        string sourceTitle,
        byte[] pngBytes,
        bool isVisualContext = false)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        VisionPreviewImage.Source = LoadBitmap(pngBytes);
        VisionSourceTitleText.Text = sourceTitle;
        VisionAttachmentTitleText.Text = isVisualContext
            ? "Contexto visual activo"
            : "Captura lista";
        VisionAttachmentHintText.Text = isVisualContext
            ? "Puedes hacer varias preguntas durante los próximos 2 minutos."
            : "Se enviará solo con tu siguiente consulta.";
        VisionAttachmentPanel.Visibility = Visibility.Visible;
        FocusPrompt();
    }

    public void ClearVisionAttachment()
    {
        if (VisionPreviewImage is not null)
        {
            VisionPreviewImage.Source = null;
        }

        if (VisionSourceTitleText is not null)
        {
            VisionSourceTitleText.Text = string.Empty;
        }

        if (VisionAttachmentPanel is not null)
        {
            VisionAttachmentPanel.Visibility = Visibility.Collapsed;
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
            MicButton.IsEnabled = available;
            MicButton.ClearValue(BackgroundProperty);
        }

        MicButton.ToolTip = detail;
        VoiceStatusText.Text = detail;
        VoiceStatusText.Visibility = available
            ? Visibility.Collapsed
            : Visibility.Visible;
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
                MicButton.IsEnabled = true;
                MicButton.Background = (Brush)FindResource("BrushAccentSoft");
                VoiceStatusText.Text = detail ?? "Escuchando… pulsa otra vez cuando termines.";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            case AssistantVoiceState.Processing:
                _voiceInputActive = false;
                MicButton.IsEnabled = false;
                VoiceStatusText.Text = detail ?? "Convirtiendo tu voz en una orden…";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            case AssistantVoiceState.Error:
                _voiceInputActive = false;
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ?? "No pude usar el micrófono.";
                VoiceStatusText.Visibility = Visibility.Visible;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;

            default:
                _voiceInputActive = false;
                MicButton.IsEnabled = _voiceAvailable;
                MicButton.ClearValue(BackgroundProperty);
                VoiceStatusText.Text = detail ??
                    "Voz local lista. Pulsa el icono para hablar y vuelve a pulsarlo para terminar.";
                VoiceStatusText.Visibility = Visibility.Collapsed;
                MicButton.ToolTip = VoiceStatusText.Text;
                break;
        }
    }

    public void BeginKohanaStreamingMessage(string placeholder = "Pensando…")
    {
        CancelKohanaStreamingMessage();

        ShowConversationSurface();
        _streamingBuffer.Clear();
        _streamingHasContent = false;
        _streamingBubble = CreateMessageBubble(
            placeholder,
            HorizontalAlignment.Left,
            (Brush)FindResource("BrushSurfaceRaised"));
        _streamingTextBlock = FindMessageTextBlock(_streamingBubble);

        ConversationPanel.Children.Add(_streamingBubble);
        ScrollConversationToEnd();
    }

    public void AppendKohanaStreamingText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_streamingBubble is null || _streamingTextBlock is null)
        {
            BeginKohanaStreamingMessage();
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

    public string CompleteKohanaStreamingMessage()
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

    public void CancelKohanaStreamingMessage()
    {
        var hadStreamingBubble = _streamingBubble is not null;
        ClearStreamingReferences(removeBubble: true);
        if (hadStreamingBubble && _messages.Count == 0)
        {
            RenderConversation();
        }
    }

    public void AddUserMessage(string text)
    {
        AddMessage(new ConversationMessage(
            ConversationRole.User,
            text,
            DateTimeOffset.Now));
    }

    public void AddKohanaMessage(string text)
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

        var hasMessages = _messages.Count > 0;
        EmptyStatePanel.Visibility = hasMessages
            ? Visibility.Collapsed
            : Visibility.Visible;
        ConversationScroll.Visibility = hasMessages
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var message in _messages)
        {
            var isUser = message.Role == ConversationRole.User;
            ConversationPanel.Children.Add(CreateMessageBubble(
                message.Text,
                isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                (Brush)FindResource(isUser ? "BrushAccentSoft" : "BrushSurfaceRaised")));
        }

        ScrollConversationToEnd();
    }

    private void ShowConversationSurface()
    {
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        ConversationScroll.Visibility = Visibility.Visible;
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

    private void VisionButton_Click(object sender, RoutedEventArgs e)
    {
        VisionCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DiscardVisionButton_Click(object sender, RoutedEventArgs e)
    {
        ClearVisionAttachment();
        VisionAttachmentCleared?.Invoke(this, EventArgs.Empty);
    }

    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_voiceInputActive)
        {
            EndVoiceInput();
            return;
        }

        BeginVoiceInput();
    }

    private void BeginVoiceInput()
    {
        if (_voiceInputActive || MicButton.IsEnabled == false)
        {
            return;
        }

        _voiceInputActive = true;
        VoiceInputStarted?.Invoke(this, EventArgs.Empty);
    }

    private void EndVoiceInput()
    {
        if (!_voiceInputActive)
        {
            return;
        }

        _voiceInputActive = false;
        VoiceInputStopped?.Invoke(this, EventArgs.Empty);
    }

    private void QuickPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string prompt } || string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PromptBox.Text = prompt;
        PromptBox.CaretIndex = PromptBox.Text.Length;
        FocusPrompt();
    }

    private void PromptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (PromptHintText is not null)
        {
            PromptHintText.Visibility = string.IsNullOrEmpty(PromptBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
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
        CancelKohanaStreamingMessage();
        _messages.Clear();
        if (HasVisionAttachment)
        {
            ClearVisionAttachment();
            VisionAttachmentCleared?.Invoke(this, EventArgs.Empty);
        }
        RenderConversation();
        ConversationCleared?.Invoke(this, EventArgs.Empty);
        FocusPrompt();
    }

    private void SubmitPrompt()
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) && HasVisionAttachment)
        {
            prompt = "Describe lo importante de esta captura y señala cualquier error visible.";
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        PromptBox.Clear();
        PromptSubmitted?.Invoke(this, new PromptSubmittedEventArgs(prompt));
        FocusPrompt();
    }

    private static BitmapImage LoadBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static Border CreateMessageBubble(
        string text,
        HorizontalAlignment alignment,
        Brush background)
    {
        var isUser = alignment == HorizontalAlignment.Right;
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20,
            Foreground = (Brush)Application.Current.FindResource("BrushTextPrimary")
        };

        return new Border
        {
            Margin = new Thickness(0, 7, 0, 0),
            Padding = new Thickness(13, 11, 13, 11),
            CornerRadius = isUser
                ? new CornerRadius(17, 17, 6, 17)
                : new CornerRadius(17, 17, 17, 6),
            Background = background,
            BorderBrush = (Brush)Application.Current.FindResource(
                isUser ? "BrushAccentBorder" : "BrushBorder"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = alignment,
            MaxWidth = isUser ? 370 : 430,
            Child = textBlock
        };
    }

    private static TextBlock? FindMessageTextBlock(DependencyObject root)
    {
        if (root is TextBlock textBlock)
        {
            return textBlock;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var found = FindMessageTextBlock(VisualTreeHelper.GetChild(root, index));
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    // Compatibilidad temporal para extensiones que todavía usan los nombres internos anteriores.
    public void BeginNexoStreamingMessage(string placeholder = "Pensando…") =>
        BeginKohanaStreamingMessage(placeholder);

    public void AppendNexoStreamingText(string text) => AppendKohanaStreamingText(text);

    public string CompleteNexoStreamingMessage() => CompleteKohanaStreamingMessage();

    public void CancelNexoStreamingMessage() => CancelKohanaStreamingMessage();

    public void AddNexoMessage(string text) => AddKohanaMessage(text);
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
