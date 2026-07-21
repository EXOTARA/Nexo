using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.Core.Settings;

namespace Nexo.App;

public partial class CapsuleWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly DispatcherTimer _dismissTimer;
    private bool _isClosingAnimation;

    public CapsuleWindow()
    {
        InitializeComponent();

        _dismissTimer = new DispatcherTimer();
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            HideAnimated();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, styles | WsExNoActivate | WsExToolWindow);
    }

    public void ShowMessage(
        CapsuleKind kind,
        string title,
        string detail,
        SidebarPosition sidebarPosition,
        TimeSpan? duration = null)
    {
        _dismissTimer.Stop();
        _isClosingAnimation = false;

        TitleText.Text = title;
        DetailText.Text = detail;
        ApplyKind(kind);
        PositionWindow(sidebarPosition);

        CapsuleBorder.BeginAnimation(OpacityProperty, null);
        CapsuleTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;
        CapsuleBorder.Opacity = 0;
        CapsuleTranslate.Y = -12;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animationDuration = TimeSpan.FromMilliseconds(155);

        CapsuleBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, animationDuration) { EasingFunction = easing });

        CapsuleTranslate.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(0, animationDuration) { EasingFunction = easing });

        _dismissTimer.Interval = duration ?? GetDefaultDuration(kind);
        _dismissTimer.Start();
    }

    public void HideImmediately()
    {
        _dismissTimer.Stop();
        _isClosingAnimation = false;
        Hide();
    }

    private void HideAnimated()
    {
        if (!IsVisible || _isClosingAnimation)
        {
            return;
        }

        _isClosingAnimation = true;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(135);

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };

        opacityAnimation.Completed += (_, _) =>
        {
            Hide();
            _isClosingAnimation = false;
        };

        CapsuleBorder.BeginAnimation(OpacityProperty, opacityAnimation);
        CapsuleTranslate.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(-8, duration) { EasingFunction = easing });
    }

    private void PositionWindow(SidebarPosition sidebarPosition)
    {
        _ = sidebarPosition;

        var workArea = SystemParameters.WorkArea;
        Top = workArea.Top + 24;
        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
    }

    private void ApplyKind(CapsuleKind kind)
    {
        var accentBrush = (Brush)FindResource("BrushAccent");
        var accentSoftBrush = (Brush)FindResource("BrushAccentSoft");

        switch (kind)
        {
            case CapsuleKind.Processing:
                StatusIcon.Text = "✦";
                StatusIcon.Foreground = accentBrush;
                StatusBadge.Background = accentSoftBrush;
                break;
            case CapsuleKind.Success:
                StatusIcon.Text = "✓";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#67D9A2"));
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#203A32"));
                break;
            case CapsuleKind.Warning:
                StatusIcon.Text = "!";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3C969"));
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3422"));
                break;
            case CapsuleKind.Error:
                StatusIcon.Text = "×";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7D8A"));
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40262C"));
                break;
            default:
                StatusIcon.Text = "i";
                StatusIcon.Foreground = accentBrush;
                StatusBadge.Background = accentSoftBrush;
                break;
        }
    }

    private static TimeSpan GetDefaultDuration(CapsuleKind kind) => kind switch
    {
        CapsuleKind.Processing => TimeSpan.FromSeconds(4),
        CapsuleKind.Error => TimeSpan.FromSeconds(4),
        CapsuleKind.Warning => TimeSpan.FromSeconds(3.4),
        _ => TimeSpan.FromSeconds(2.7)
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);
}

public enum CapsuleKind
{
    Information,
    Processing,
    Success,
    Warning,
    Error
}
