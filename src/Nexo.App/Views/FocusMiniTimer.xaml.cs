using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Nexo.App.DailyFlow;

namespace Nexo.App.Views;

/// <summary>
/// Diseño D3.1 — control de presentación pura: recibe un <see cref="FocusDisplayState"/> ya
/// calculado y emite intenciones. No sabe qué es <c>FocusManager</c>.
/// </summary>
public partial class FocusMiniTimer : UserControl
{
    private bool _isPaused;

    public FocusMiniTimer()
    {
        InitializeComponent();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? PauseRequested;

    public event EventHandler? ResumeRequested;

    public event EventHandler? FinishRequested;

    public void Apply(FocusDisplayState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Visibility = state.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (!state.IsVisible)
        {
            return;
        }

        LabelText.Text = state.AssociatedLabel ?? "Enfoque";
        ClockText.Text = state.ClockText;
        PausedBadge.Visibility = state.IsPaused ? Visibility.Visible : Visibility.Collapsed;

        _isPaused = state.IsPaused;
        PauseResumeGlyph.Text = _isPaused ? "▶" : "⏸";
        PauseResumeButton.ToolTip = _isPaused ? "Continuar sesión de enfoque" : "Pausar sesión de enfoque";
        AutomationProperties.SetName(
            PauseResumeButton,
            _isPaused ? "Continuar sesión de enfoque desde el mini temporizador" : "Pausar sesión de enfoque desde el mini temporizador");
        PauseResumeButton.IsEnabled = state.CanPause || state.CanResume;
        FinishButton.IsEnabled = state.CanFinish;

        var taskDescription = state.AssociatedLabel is null
            ? "Sesión de enfoque en curso"
            : $"Sesión de enfoque: {state.AssociatedLabel}";
        AutomationProperties.SetName(
            RootBorder,
            $"{taskDescription}. {state.StatusText}. Quedan {state.ClockText}.");
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) =>
        OpenRequested?.Invoke(this, EventArgs.Empty);

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e) =>
        FinishRequested?.Invoke(this, EventArgs.Empty);
}
