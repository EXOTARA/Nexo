using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nexo.App.Views;

public partial class AssistantView : UserControl
{
    public event EventHandler<PromptSubmittedEventArgs>? PromptSubmitted;

    public AssistantView()
    {
        InitializeComponent();
    }

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
        ConversationPanel.Children.Add(CreateMessageBubble(
            text,
            HorizontalAlignment.Right,
            (Brush)FindResource("BrushAccentSoft")));
        ConversationScroll.ScrollToEnd();
    }

    public void AddNexoMessage(string text)
    {
        ConversationPanel.Children.Add(CreateMessageBubble(
            text,
            HorizontalAlignment.Left,
            (Brush)FindResource("BrushSurfaceRaised")));
        ConversationScroll.ScrollToEnd();
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
