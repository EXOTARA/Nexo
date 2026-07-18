using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4E58;
    private const uint ModAlt = 0x0001;
    private const uint VirtualKeyA = 0x41;
    private const int WmHotkey = 0x0312;

    private readonly DispatcherTimer _clockTimer;
    private HwndSource? _windowSource;
    private bool _isHiding;

    public MainWindow()
    {
        InitializeComponent();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        if (!RegisterHotKey(windowHandle, HotkeyId, ModAlt, VirtualKeyA))
        {
            AddSystemMessage("Alt + A ya está siendo utilizado por otra aplicación.");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateClock();
        _clockTimer.Start();
        ShowAnimated();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, HotkeyId);
        }

        _windowSource?.RemoveHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ToggleWindow()
    {
        if (IsVisible && Opacity > 0.1 && !_isHiding)
        {
            HideAnimated();
            return;
        }

        ShowAnimated();
    }

    private void ShowAnimated()
    {
        _isHiding = false;
        PositionWindow();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;

        ShellTranslate.X = 34;
        ShellBorder.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(170);

        ShellTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, duration) { EasingFunction = easing });

        ShellBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = easing });

        Dispatcher.BeginInvoke(() =>
        {
            PromptBox.Focus();
            Keyboard.Focus(PromptBox);
        }, DispatcherPriority.Input);
    }

    private void HideAnimated()
    {
        if (_isHiding)
        {
            return;
        }

        _isHiding = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(140);

        var slideAnimation = new DoubleAnimation(34, duration)
        {
            EasingFunction = easing
        };

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };

        opacityAnimation.Completed += (_, _) =>
        {
            Hide();
            _isHiding = false;
        };

        ShellTranslate.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
        ShellBorder.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Height = Math.Max(MinHeight, workArea.Height - 24);
        Left = workArea.Right - Width - 12;
        Top = workArea.Top + 12;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("dddd, d 'de' MMMM", new CultureInfo("es-MX"));
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAnimated();
            e.Handled = true;
        }
    }

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            SendPrompt();
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendPrompt();
    }

    private void SendPrompt()
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        AddUserMessage(prompt);
        PromptBox.Clear();
        AddSystemMessage("El motor de comandos e IA se conectará en el siguiente sprint.");
        ConversationScroll.ScrollToEnd();
        PromptBox.Focus();
    }

    private void AddUserMessage(string text)
    {
        ConversationPanel.Children.Add(CreateMessageBubble(
            text,
            horizontalAlignment: HorizontalAlignment.Right,
            background: (Brush)FindResource("BrushAccentSoft")));
    }

    private void AddSystemMessage(string text)
    {
        ConversationPanel.Children.Add(CreateMessageBubble(
            text,
            horizontalAlignment: HorizontalAlignment.Left,
            background: (Brush)FindResource("BrushSurfaceRaised")));
    }

    private static Border CreateMessageBubble(
        string text,
        HorizontalAlignment horizontalAlignment,
        Brush background)
    {
        return new Border
        {
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(13),
            Background = background,
            HorizontalAlignment = horizontalAlignment,
            MaxWidth = 310,
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White
            }
        };
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string destination)
        {
            return;
        }

        FrameworkElement targetView = destination switch
        {
            "Audio" => AudioView,
            "Capture" => CaptureView,
            "System" => SystemView,
            _ => AssistantView
        };

        ShowView(targetView);
        UpdateNavigationState(button);
    }

    private void ShowView(FrameworkElement targetView)
    {
        FrameworkElement[] views =
        [
            AssistantView,
            AudioView,
            CaptureView,
            SystemView
        ];

        foreach (var view in views)
        {
            if (!ReferenceEquals(view, targetView))
            {
                view.Visibility = Visibility.Collapsed;
                view.Opacity = 0;
            }
        }

        targetView.Visibility = Visibility.Visible;
        targetView.Opacity = 0;

        var transform = new TranslateTransform(16, 0);
        targetView.RenderTransform = transform;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(145);

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, duration) { EasingFunction = easing });

        targetView.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = easing });
    }

    private void UpdateNavigationState(Button selectedButton)
    {
        Button[] buttons =
        [
            AssistantNavButton,
            AudioNavButton,
            CaptureNavButton,
            SystemNavButton
        ];

        foreach (var button in buttons)
        {
            var isSelected = ReferenceEquals(button, selectedButton);
            button.Background = isSelected
                ? (Brush)FindResource("BrushAccentSoft")
                : Brushes.Transparent;
            button.Foreground = isSelected
                ? (Brush)FindResource("BrushTextPrimary")
                : (Brush)FindResource("BrushTextSecondary");
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
