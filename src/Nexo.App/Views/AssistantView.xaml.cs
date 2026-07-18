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
    private bool _saveHistory;
    private int _recentMessageLimit = 8;

    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;
    public event EventHandler? ConversationChanged;
    public event EventHandler? ConversationCleared;

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

        Dispatcher.BeginInvoke(
            new Action(ConversationScroll.ScrollToEnd),
            DispatcherPriority.Background);
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

public sealed class PromptSubmittedEventArgs : EventArgs
{
    public PromptSubmittedEventArgs(string prompt)
    {
        Prompt = prompt;
    }

    public string Prompt { get; }
}
